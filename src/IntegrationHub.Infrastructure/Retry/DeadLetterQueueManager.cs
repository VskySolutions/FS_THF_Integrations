using IntegrationHub.Application.Abstractions.Auditing;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Retry;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Infrastructure.Retry;

/// <summary>
/// Atomic dead-letter transition: marks the job <c>PermanentlyFailed</c>, writes a final
/// audit entry capturing the failure reason and retry history, and removes the retry
/// queue row — committed in a single transaction (AC-INF-003.2).
/// </summary>
internal sealed class DeadLetterQueueManager : IDeadLetterQueueManager
{
    private readonly IIntegrationJobRepository _jobRepository;
    private readonly IRetryQueueRepository _retryQueueRepository;
    private readonly IAuditTrailService _auditTrailService;
    private readonly IUnitOfWork _unitOfWork;

    public DeadLetterQueueManager(
        IIntegrationJobRepository jobRepository,
        IRetryQueueRepository retryQueueRepository,
        IAuditTrailService auditTrailService,
        IUnitOfWork unitOfWork)
    {
        _jobRepository = jobRepository;
        _retryQueueRepository = retryQueueRepository;
        _auditTrailService = auditTrailService;
        _unitOfWork = unitOfWork;
    }

    public async Task MoveToDeadLetterAsync(
        IntegrationJob job,
        RetryQueueEntry? retryEntry,
        string reason,
        CancellationToken cancellationToken = default)
    {
        job.Status = IntegrationJobStatus.PermanentlyFailed;
        job.CompletedAtUtc = DateTime.UtcNow;
        job.ErrorMessage = reason;
        _jobRepository.Update(job);

        var details = retryEntry is null
            ? reason
            : $"{reason} | attempts={retryEntry.RetryCount}";

        await _auditTrailService.AddAsync(
            entityName: nameof(IntegrationJob),
            entityId: job.Id.ToString(),
            action: "DeadLettered",
            details: details,
            cancellationToken: cancellationToken);

        if (retryEntry is not null)
        {
            _retryQueueRepository.Remove(retryEntry);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
