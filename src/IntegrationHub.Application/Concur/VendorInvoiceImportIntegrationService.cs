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
/// Vendor invoice import flow (Concur → Maconomy): fetch invoices → validate → transform →
/// write, with per-record independence and partial-failure tracking (REQ-CON-003/004).
/// </summary>
public sealed class VendorInvoiceImportIntegrationService
{
    private readonly IConcurConnector _concur;
    private readonly IMaconomyConnector _maconomy;
    private readonly ConcurInvoiceTransformer _transformer;
    private readonly InvoiceValidator _validator;
    private readonly IIntegrationJobRepository _jobs;
    private readonly IIntegrationLogRepository _logs;
    private readonly IRetryQueueManager _retry;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<VendorInvoiceImportIntegrationService> _logger;

    public VendorInvoiceImportIntegrationService(
        IConcurConnector concur,
        IMaconomyConnector maconomy,
        ConcurInvoiceTransformer transformer,
        InvoiceValidator validator,
        IIntegrationJobRepository jobs,
        IIntegrationLogRepository logs,
        IRetryQueueManager retry,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork,
        ILogger<VendorInvoiceImportIntegrationService> logger)
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

        var fetch = await _concur.GetVendorInvoicesAsync(cancellationToken);
        if (!fetch.Success || fetch.Payload is null)
        {
            await _retry.RegisterFailureAsync(job.Id, fetch.IsRetriable, $"Fetch: {fetch.ErrorMessage}", cancellationToken);
            return IntegrationFlowResult.Fail("Fetch", fetch.ErrorMessage ?? "Fetch failed.", fetch.IsRetriable);
        }

        int succeeded = 0, transient = 0, invalid = 0, duplicates = 0;

        foreach (var invoice in fetch.Payload)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var validation = _validator.Validate(invoice);
            if (!validation.IsValid)
            {
                invalid++;
                await LogAsync(job, "Warning", $"Validation failed for invoice {invoice.InvoiceNumber}.", cancellationToken);
                continue;
            }

            var transform = await _transformer.TransformAsync(invoice, cancellationToken);
            if (!transform.Success || transform.Payload is null)
            {
                invalid++;
                await LogAsync(job, "Error", $"Transform failed for invoice {invoice.InvoiceNumber}.", cancellationToken);
                continue;
            }

            var write = await _maconomy.WriteVendorInvoiceAsync(transform.Payload, cancellationToken);
            if (write.Success && write.Payload!.Duplicate)
            {
                duplicates++;
                _logger.LogWarning("Vendor invoice {InvoiceNumber} already exists — skipped", invoice.InvoiceNumber);
                await LogAsync(job, "Warning", $"Duplicate invoice {invoice.InvoiceNumber} skipped.", cancellationToken);
                continue;
            }

            if (!write.Success)
            {
                if (write.IsRetriable) { transient++; } else { invalid++; }
                await LogAsync(job, "Error", $"Write failed for invoice {invoice.InvoiceNumber}: {write.ErrorMessage}", cancellationToken);
                continue;
            }

            succeeded++;
            await LogAsync(job, "Information", $"Invoice {invoice.InvoiceNumber} imported.", cancellationToken);
        }

        var summary = $"succeeded={succeeded}; transient={transient}; invalid={invalid}; duplicates={duplicates}";
        if (transient + invalid == 0)
        {
            job.Status = IntegrationJobStatus.Completed;
            job.CompletedAtUtc = DateTime.UtcNow;
            _jobs.Update(job);
            await _audit.AddAsync(nameof(IntegrationJob), job.Id.ToString(), "InvoiceImportCompleted", details: summary, cancellationToken: cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return IntegrationFlowResult.Ok();
        }

        var retriable = transient > 0;
        await _retry.RegisterFailureAsync(job.Id, retriable, summary, cancellationToken);
        if (succeeded > 0)
        {
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

    private Task LogAsync(IntegrationJob job, string level, string message, CancellationToken cancellationToken)
        => _logs.AddAsync(new IntegrationLog { JobId = job.Id, Level = level, Message = message, CreatedAtUtc = DateTime.UtcNow }, cancellationToken);
}
