using IntegrationHub.Application.Abstractions.Auditing;
using IntegrationHub.Application.Abstractions.Connectors.Concur;
using IntegrationHub.Application.Abstractions.Connectors.Maconomy;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Retry;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Shared.Connectors;
using Microsoft.Extensions.Logging;

namespace IntegrationHub.Application.Concur;

/// <summary>
/// Expense report import flow (Concur → Maconomy): fetch approved reports → validate →
/// transform → write. Each report is processed independently; transient write failures
/// register the job for retry, validation failures are non-retriable, duplicates are
/// skipped, and a mixed batch marks the job PartiallyFailed (REQ-CON-001/002).
/// </summary>
public sealed class ExpenseImportIntegrationService
{
    private readonly IConcurConnector _concur;
    private readonly IMaconomyConnector _maconomy;
    private readonly ConcurExpenseTransformer _transformer;
    private readonly ExpenseValidator _validator;
    private readonly IIntegrationJobRepository _jobs;
    private readonly IIntegrationLogRepository _logs;
    private readonly IRetryQueueManager _retry;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ExpenseImportIntegrationService> _logger;

    public ExpenseImportIntegrationService(
        IConcurConnector concur,
        IMaconomyConnector maconomy,
        ConcurExpenseTransformer transformer,
        ExpenseValidator validator,
        IIntegrationJobRepository jobs,
        IIntegrationLogRepository logs,
        IRetryQueueManager retry,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork,
        ILogger<ExpenseImportIntegrationService> logger)
    {
        _concur = concur;
        _maconomy = maconomy;
        _transformer = transformer;
        _validator = validator;
        _jobs = jobs;
        _logs = logs;
        _retry = retry;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IntegrationFlowResult> ExecuteAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _jobs.GetByIdAsync(jobId, cancellationToken);
        if (job is null)
        {
            return IntegrationFlowResult.Fail("Load", "Job not found.", isRetriable: false);
        }

        job.Status = IntegrationJobStatus.Running;
        job.StartedAtUtc = DateTime.UtcNow;
        _jobs.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var fetch = await _concur.GetApprovedExpenseReportsAsync(cancellationToken);
        if (!fetch.Success || fetch.Payload is null)
        {
            return await FailAsync(job, "Fetch", fetch.ErrorMessage ?? "Fetch failed.", fetch.IsRetriable, cancellationToken);
        }

        int succeeded = 0, transientFailures = 0, validationFailures = 0, duplicates = 0;

        foreach (var report in fetch.Payload)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var validation = _validator.Validate(report);
            if (!validation.IsValid)
            {
                validationFailures++;
                await WriteLogAsync(job, "Warning", $"Validation failed for report {report.ReportId}: "
                    + string.Join("; ", validation.Violations.Select(v => $"{v.Field}: {v.Message}")), cancellationToken);
                continue;
            }

            var transform = await _transformer.TransformAsync(report, job.InterfaceName, cancellationToken);
            if (!transform.Success || transform.Payload is null)
            {
                validationFailures++;
                await WriteLogAsync(job, "Error", $"Transform failed for report {report.ReportId}.", cancellationToken);
                continue;
            }

            var write = await _maconomy.WriteExpenseReportAsync(transform.Payload, cancellationToken);
            if (write.Success && write.Payload!.Duplicate)
            {
                duplicates++;
                _logger.LogWarning("Expense report {ReportId} already exists in Maconomy — skipped", report.ReportId);
                await WriteLogAsync(job, "Warning", $"Duplicate report {report.ReportId} skipped.", cancellationToken);
                continue;
            }

            if (!write.Success)
            {
                transientFailures += write.IsRetriable ? 1 : 0;
                validationFailures += write.IsRetriable ? 0 : 1;
                await WriteLogAsync(job, "Error", $"Write failed for report {report.ReportId}: {write.ErrorMessage}", cancellationToken);
                continue;
            }

            succeeded++;
            await WriteLogAsync(job, "Information", $"Report {report.ReportId} imported.", cancellationToken);
        }

        return await FinalizeAsync(job, succeeded, transientFailures, validationFailures, duplicates, cancellationToken);
    }

    private async Task<IntegrationFlowResult> FinalizeAsync(
        IntegrationJob job, int succeeded, int transientFailures, int validationFailures, int duplicates, CancellationToken cancellationToken)
    {
        var totalFailures = transientFailures + validationFailures;
        var summary = $"succeeded={succeeded}; transientFailures={transientFailures}; validationFailures={validationFailures}; duplicates={duplicates}";

        if (totalFailures == 0)
        {
            job.Status = IntegrationJobStatus.Completed;
            job.CompletedAtUtc = DateTime.UtcNow;
            _jobs.Update(job);
            await _audit.AddAsync(nameof(IntegrationJob), job.Id.ToString(), "ExpenseImportCompleted", details: summary, cancellationToken: cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return IntegrationFlowResult.Ok();
        }

        // Register the job for retry when any failure was transient; otherwise mark non-retriable.
        var retriable = transientFailures > 0;
        await _retry.RegisterFailureAsync(job.Id, retriable, summary, cancellationToken);

        if (succeeded > 0)
        {
            // Mixed outcome: keep the successes, flag the job partially failed.
            var reloaded = await _jobs.GetByIdAsync(job.Id, cancellationToken);
            if (reloaded is not null)
            {
                reloaded.Status = IntegrationJobStatus.PartiallyFailed;
                _jobs.Update(reloaded);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        return IntegrationFlowResult.Fail("Write", summary, retriable);
    }

    private async Task<IntegrationFlowResult> FailAsync(IntegrationJob job, string step, string error, bool retriable, CancellationToken cancellationToken)
    {
        await _retry.RegisterFailureAsync(job.Id, retriable, $"{step}: {error}", cancellationToken);
        return IntegrationFlowResult.Fail(step, error, retriable);
    }

    private Task WriteLogAsync(IntegrationJob job, string level, string message, CancellationToken cancellationToken)
        => _logs.AddAsync(new IntegrationLog
        {
            JobId = job.Id,
            Level = level,
            Message = message,
        }, cancellationToken);
}
