using IntegrationHub.Application.Abstractions.Auditing;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Retry;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Shared.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntegrationHub.Infrastructure.Retry;

/// <summary>
/// Single retry-logic path. Routes validation failures straight to the dead-letter
/// queue and transient failures into the retry queue with incremental backoff, dead-
/// lettering once the configured attempt count is exhausted (REQ-INF-003).
/// </summary>
internal sealed class RetryQueueManager : IRetryQueueManager
{
    private readonly IIntegrationJobRepository _jobRepository;
    private readonly IRetryQueueRepository _retryQueueRepository;
    private readonly IDeadLetterQueueManager _deadLetterQueueManager;
    private readonly IIntegrationJobExecutor _jobExecutor;
    private readonly IAuditTrailService _auditTrailService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly RetryOptions _options;
    private readonly ILogger<RetryQueueManager> _logger;

    public RetryQueueManager(
        IIntegrationJobRepository jobRepository,
        IRetryQueueRepository retryQueueRepository,
        IDeadLetterQueueManager deadLetterQueueManager,
        IIntegrationJobExecutor jobExecutor,
        IAuditTrailService auditTrailService,
        IUnitOfWork unitOfWork,
        IOptions<RetryOptions> options,
        ILogger<RetryQueueManager> logger)
    {
        _jobRepository = jobRepository;
        _retryQueueRepository = retryQueueRepository;
        _deadLetterQueueManager = deadLetterQueueManager;
        _jobExecutor = jobExecutor;
        _auditTrailService = auditTrailService;
        _unitOfWork = unitOfWork;
        _options = options.Value;
        _logger = logger;
    }

    public async Task RegisterFailureAsync(Guid jobId, bool isRetriable, string? error, CancellationToken cancellationToken = default)
    {
        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken);
        if (job is null)
        {
            _logger.LogWarning("RegisterFailure called for unknown job {JobId}", jobId);
            return;
        }

        var reason = string.IsNullOrWhiteSpace(error) ? "Unspecified failure" : error;
        var existingEntry = await _retryQueueRepository.GetByJobIdAsync(jobId, cancellationToken);

        // Validation failures are never retried (AC-INF-003.4).
        if (!isRetriable)
        {
            await _deadLetterQueueManager.MoveToDeadLetterAsync(job, existingEntry, reason, cancellationToken);
            return;
        }

        var nextAttempt = (existingEntry?.RetryCount ?? 0) + 1;

        // Retries exhausted — dead-letter (AC-INF-003.2).
        if (nextAttempt > _options.MaxAttempts)
        {
            await _deadLetterQueueManager.MoveToDeadLetterAsync(job, existingEntry, reason, cancellationToken);
            return;
        }

        var nextRetryDate = DateTime.UtcNow.Add(_options.GetBackoff(nextAttempt));

        if (existingEntry is null)
        {
            await _retryQueueRepository.AddAsync(new RetryQueueEntry
            {
                Id = Guid.NewGuid(),
                JobId = jobId,
                RetryCount = nextAttempt,
                NextRetryDate = nextRetryDate,
                Status = RetryStatus.Pending,
                LastError = reason,
                CreatedAtUtc = DateTime.UtcNow,
            }, cancellationToken);
        }
        else
        {
            existingEntry.RetryCount = nextAttempt;
            existingEntry.NextRetryDate = nextRetryDate;
            existingEntry.Status = RetryStatus.Pending;
            existingEntry.LastError = reason;
            _retryQueueRepository.Update(existingEntry);
        }

        job.Status = IntegrationJobStatus.Failed;
        job.AttemptCount = nextAttempt;
        job.ErrorMessage = reason;
        _jobRepository.Update(job);

        // Audit the retry enqueue in the same transaction as the state change.
        await _auditTrailService.AddAsync(
            entityName: nameof(IntegrationJob),
            entityId: jobId.ToString(),
            action: "RetryScheduled",
            details: $"attempt={nextAttempt}; nextRetry={nextRetryDate:o}",
            cancellationToken: cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Job {JobId} scheduled for retry attempt {Attempt} at {NextRetryDate:o}",
            jobId, nextAttempt, nextRetryDate);
    }

    public async Task<bool> ManualRetryAsync(Guid jobId, string? performedBy, CancellationToken cancellationToken = default)
    {
        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken);
        if (job is null)
        {
            return false;
        }

        // Only failed jobs can be manually retried (AC-INF-004.2).
        if (job.Status is not (IntegrationJobStatus.Failed or IntegrationJobStatus.PermanentlyFailed or IntegrationJobStatus.PartiallyFailed))
        {
            return false;
        }

        var entry = await _retryQueueRepository.GetByJobIdAsync(jobId, cancellationToken);
        if (entry is not null)
        {
            _retryQueueRepository.Remove(entry); // reset retry state
        }

        job.Status = IntegrationJobStatus.Created;
        job.AttemptCount = 0;
        job.ErrorMessage = null;
        _jobRepository.Update(job);

        await _auditTrailService.AddAsync(
            entityName: nameof(IntegrationJob),
            entityId: jobId.ToString(),
            action: "ManualRetry",
            details: "Retry count reset and re-enqueued.",
            performedBy: performedBy,
            cancellationToken: cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _jobExecutor.EnqueueForExecutionAsync(jobId, cancellationToken);

        _logger.LogInformation("Job {JobId} manually retried by {Actor}", jobId, performedBy ?? "system");
        return true;
    }

    public async Task ProcessDueRetriesAsync(CancellationToken cancellationToken = default)
    {
        var due = await _retryQueueRepository.ListDueAsync(DateTime.UtcNow, cancellationToken);
        _logger.LogInformation("Processing {Count} due retry entries", due.Count);

        foreach (var entry in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _jobExecutor.EnqueueForExecutionAsync(entry.JobId, cancellationToken);
            _logger.LogInformation(
                "Re-enqueued job {JobId} for retry attempt {Attempt}",
                entry.JobId, entry.RetryCount);
        }
    }
}
