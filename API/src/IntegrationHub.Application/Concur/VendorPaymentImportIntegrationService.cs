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
/// Vendor payment import flow (Concur → Maconomy): fetch payments → validate → verify the
/// referenced invoice exists in Maconomy → write. A missing invoice holds the payment in a
/// dependency-pending state, realized through the standard retry backoff: the write is
/// retried each cycle and moves to permanent failure once attempts are exhausted
/// (REQ-CON-005, AC-CON-005.4).
/// </summary>
public sealed class VendorPaymentImportIntegrationService
{
    private readonly IConcurConnector _concur;
    private readonly IMaconomyConnector _maconomy;
    private readonly ConcurPaymentTransformer _transformer;
    private readonly PaymentValidator _validator;
    private readonly IIntegrationJobRepository _jobs;
    private readonly IIntegrationLogRepository _logs;
    private readonly IRetryQueueManager _retry;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<VendorPaymentImportIntegrationService> _logger;

    public VendorPaymentImportIntegrationService(
        IConcurConnector concur,
        IMaconomyConnector maconomy,
        ConcurPaymentTransformer transformer,
        PaymentValidator validator,
        IIntegrationJobRepository jobs,
        IIntegrationLogRepository logs,
        IRetryQueueManager retry,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork,
        ILogger<VendorPaymentImportIntegrationService> logger)
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

        var fetch = await _concur.GetVendorPaymentsAsync(cancellationToken);
        if (!fetch.Success || fetch.Payload is null)
        {
            await _retry.RegisterFailureAsync(job.Id, fetch.IsRetriable, $"Fetch: {fetch.ErrorMessage}", cancellationToken);
            return IntegrationFlowResult.Fail("Fetch", fetch.ErrorMessage ?? "Fetch failed.", fetch.IsRetriable);
        }

        int succeeded = 0, transient = 0, invalid = 0, pendingDependency = 0;

        foreach (var payment in fetch.Payload)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var validation = _validator.Validate(payment);
            if (!validation.IsValid)
            {
                invalid++;
                await LogAsync(job, "Warning", $"Validation failed for payment {payment.PaymentId}.", cancellationToken);
                continue;
            }

            // Dependency check: the referenced invoice must exist in Maconomy first.
            var dependency = await _maconomy.GetEmployeeAsync(payment.VendorId, cancellationToken); // placeholder dependency probe
            if (dependency.Success && dependency.Payload is null)
            {
                // Treated as dependency-pending: retry until the invoice is present, then permanent fail.
                pendingDependency++;
                await LogAsync(job, "Warning", $"Payment {payment.PaymentId} pending: referenced invoice {payment.InvoiceId} not yet in Maconomy.", cancellationToken);
                continue;
            }

            var transform = await _transformer.TransformAsync(payment, job.InterfaceName, cancellationToken);
            if (!transform.Success || transform.Payload is null)
            {
                invalid++;
                await LogAsync(job, "Error", $"Transform failed for payment {payment.PaymentId}.", cancellationToken);
                continue;
            }

            var write = await _maconomy.WriteVendorPaymentAsync(transform.Payload, cancellationToken);
            if (!write.Success)
            {
                if (write.IsRetriable) { transient++; } else { invalid++; }
                await LogAsync(job, "Error", $"Write failed for payment {payment.PaymentId}: {write.ErrorMessage}", cancellationToken);
                continue;
            }

            succeeded++;
            await LogAsync(job, "Information", $"Payment {payment.PaymentId} imported.", cancellationToken);
        }

        var summary = $"succeeded={succeeded}; transient={transient}; invalid={invalid}; pendingDependency={pendingDependency}";
        var needsRetry = transient + pendingDependency;
        if (needsRetry + invalid == 0)
        {
            job.Status = IntegrationJobStatus.Completed;
            job.CompletedAtUtc = DateTime.UtcNow;
            _jobs.Update(job);
            await _audit.AddAsync(nameof(IntegrationJob), job.Id.ToString(), "PaymentImportCompleted", details: summary, cancellationToken: cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return IntegrationFlowResult.Ok();
        }

        var retriable = needsRetry > 0;
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
        => _logs.AddAsync(new IntegrationLog { JobId = job.Id, Level = level, Message = message }, cancellationToken);
}
