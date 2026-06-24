using global::Hangfire;
using IntegrationHub.Application.Abstractions.Connectors.Maconomy;
using IntegrationHub.Application.Abstractions.Email;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Tenancy;
using IntegrationHub.Application.Customers;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace IntegrationHub.Infrastructure.Jobs;

/// <summary>
/// On-demand Hangfire job that synchronises an approved <see cref="CustomerRequest"/> to Maconomy
/// using the existing <see cref="IMaconomyConnector"/>. Triggered by the approval service on final
/// approval (and by a manual Retry Sync). Reconstructs the tenant context from the payload, records
/// an <see cref="IntegrationJob"/> (so the run is visible in Integration Jobs), resolves the tenant's
/// Maconomy credentials (immediate non-retriable failure if absent), pushes the customer master
/// record, and records the outcome. Transient failures are retried with incremental backoff.
/// <para>
/// A sync only runs for a request in <see cref="CustomerRequestStatus.SyncInProgress"/> — the state
/// set when a request is fully <b>Approved</b> (or a Failed sync is manually retried). Requests in
/// any other state are skipped, so only approved customers are ever pushed to Maconomy.
/// </para>
/// </summary>
public sealed class CustomerSyncJob
{
    /// <summary>Incremental backoff (minutes) applied to transient sync failures, by attempt number.</summary>
    private static readonly int[] BackoffMinutes = { 5, 15, 30, 60 };

    private readonly ITenantContext _tenantContext;
    private readonly ITenantRepository _tenants;
    private readonly ICustomerRequestRepository _requests;
    private readonly ICustomerAuditRepository _audit;
    private readonly IIntegrationJobRepository _jobs;
    private readonly IIntegrationLogRepository _logs;
    private readonly ITenantApiConfigurationService _configurationService;
    private readonly IMaconomyConnector _maconomy;
    private readonly ICustomerMaconomyMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly IUserRepository _users;
    private readonly IEmailNotificationService _emailNotifications;
    private readonly ILogger<CustomerSyncJob> _logger;

    public CustomerSyncJob(
        ITenantContext tenantContext,
        ITenantRepository tenants,
        ICustomerRequestRepository requests,
        ICustomerAuditRepository audit,
        IIntegrationJobRepository jobs,
        IIntegrationLogRepository logs,
        ITenantApiConfigurationService configurationService,
        IMaconomyConnector maconomy,
        ICustomerMaconomyMapper mapper,
        IUnitOfWork unitOfWork,
        IBackgroundJobClient backgroundJobs,
        IUserRepository users,
        IEmailNotificationService emailNotifications,
        ILogger<CustomerSyncJob> logger)
    {
        _tenantContext = tenantContext;
        _tenants = tenants;
        _requests = requests;
        _audit = audit;
        _jobs = jobs;
        _logs = logs;
        _configurationService = configurationService;
        _maconomy = maconomy;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
        _backgroundJobs = backgroundJobs;
        _users = users;
        _emailNotifications = emailNotifications;
        _logger = logger;
    }

    public async Task RunAsync(Guid customerRequestId, Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await _tenants.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            _logger.LogWarning("CustomerSyncJob: tenant {Tenant} not found for request {Request}", tenantId, customerRequestId);
            return;
        }

        // Reconstruct the tenant context so credential resolution + persistence are scoped correctly.
        _tenantContext.Set(tenant.Id, tenant.Identifier);

        var request = await _requests.GetByIdForTenantAsync(customerRequestId, tenantId, cancellationToken);
        if (request is null)
        {
            _logger.LogWarning("CustomerSyncJob: request {Request} not found for tenant {Tenant}", customerRequestId, tenantId);
            return;
        }

        // Only an Approved (then queued) request syncs. Anything else is skipped — guards against
        // syncing a customer that has not been fully approved.
        if (request.Status != CustomerRequestStatus.SyncInProgress)
        {
            _logger.LogWarning(
                "CustomerSyncJob: request {Request} is {Status}, not awaiting sync — skipping.", request.Id, request.Status);
            return;
        }

        // Record an Integration Job for this run so it appears in /jobs and logs.
        var job = await CreateJobAsync(request, cancellationToken);
        await AppendAuditAsync(request, CustomerAuditActionType.SyncStarted, "Maconomy synchronisation started.", cancellationToken);

        // REQ-CUS-016.2: missing credentials → immediate, non-retriable failure.
        var config = await _configurationService.GetMaconomyConfigAsync(cancellationToken);
        if (config is null)
        {
            await FailAsync(request, job, "Maconomy credentials not configured for tenant", terminal: true, cancellationToken);
            return;
        }

        request.SyncAttempts++;
        job.AttemptCount = request.SyncAttempts;

        // Build the payload via the tenant's configurable CustomerSync field mapping (defaults applied for unmapped fields).
        var payload = await _mapper.BuildAsync(request, cancellationToken);
        var result = await _maconomy.CreateCustomerAsync(payload, cancellationToken);

        if (result.Success)
        {
            request.MaconomyCustomerNumber = result.Payload!.EntityId;
            request.Status = CustomerRequestStatus.Synced;
            request.LastSyncError = null;
            await AppendAuditAsync(request, CustomerAuditActionType.Synced,
                $"Synced to Maconomy as customer {request.MaconomyCustomerNumber}.", cancellationToken);
            await CompleteJobAsync(job, IntegrationJobStatus.Completed, null,
                $"Customer synced to Maconomy as {request.MaconomyCustomerNumber}.", cancellationToken);
            _requests.Update(request);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("CustomerSyncJob: request {Request} synced as {Number}", request.Id, request.MaconomyCustomerNumber);

            await NotifySubmitterAsync(request, EmailTemplateKey.CustomerSynced, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["MaconomyCustomerNumber"] = request.MaconomyCustomerNumber,
            }, cancellationToken);
            return;
        }

        // Transient failure with retries remaining → reschedule with backoff; otherwise mark Failed.
        if (result.IsRetriable && request.SyncAttempts < BackoffMinutes.Length)
        {
            var delay = TimeSpan.FromMinutes(BackoffMinutes[request.SyncAttempts]);
            request.LastSyncError = result.ErrorMessage;
            await AppendAuditAsync(request, CustomerAuditActionType.SyncFailed,
                $"Attempt {request.SyncAttempts} failed: {result.ErrorMessage}. Retrying in {delay.TotalMinutes:N0} min.", cancellationToken);
            // This run's job is a (retry-eligible) failure; the rescheduled run records its own job.
            await CompleteJobAsync(job, IntegrationJobStatus.Failed, result.ErrorMessage,
                $"Attempt {request.SyncAttempts} failed; retrying in {delay.TotalMinutes:N0} min.", cancellationToken);
            _requests.Update(request);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _backgroundJobs.Schedule<CustomerSyncJob>(j => j.RunAsync(request.Id, tenantId, CancellationToken.None), delay);
            return;
        }

        await FailAsync(request, job, result.ErrorMessage ?? "Maconomy sync failed.", terminal: true, cancellationToken);
    }

    private async Task FailAsync(CustomerRequest request, IntegrationJob job, string error, bool terminal, CancellationToken cancellationToken)
    {
        request.Status = CustomerRequestStatus.Failed;
        request.LastSyncError = error;
        await AppendAuditAsync(request, CustomerAuditActionType.SyncFailed,
            request.SyncAttempts > 0 ? $"Sync failed after {request.SyncAttempts} attempt(s): {error}" : error, cancellationToken);
        await CompleteJobAsync(job, terminal ? IntegrationJobStatus.PermanentlyFailed : IntegrationJobStatus.Failed, error,
            $"Sync failed: {error}", cancellationToken);
        _requests.Update(request);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogError("CustomerSyncJob: request {Request} for tenant {Tenant} marked Failed: {Error}", request.Id, request.TenantId, error);

        await NotifySubmitterAsync(request, EmailTemplateKey.CustomerSyncFailed, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ErrorMessage"] = error,
        }, cancellationToken);
    }

    /// <summary>Emails the request's submitter a workflow template (best-effort; no-op when there's no submitter/email).</summary>
    private async Task NotifySubmitterAsync(CustomerRequest request, EmailTemplateKey key, IReadOnlyDictionary<string, string?> extra, CancellationToken cancellationToken)
    {
        if (request.SubmittedById is not { } sid)
        {
            return;
        }
        var submitter = await _users.GetByIdAsync(sid, cancellationToken);
        if (submitter is null || string.IsNullOrWhiteSpace(submitter.Email))
        {
            return;
        }

        var model = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["SubmitterName"] = submitter.DisplayName,
            ["CustomerName"] = string.IsNullOrWhiteSpace(request.CompanyName) ? request.LegalName : request.CompanyName,
            ["CustomerRequestNumber"] = request.CustomerRequestNumber,
        };
        foreach (var kv in extra)
        {
            model[kv.Key] = kv.Value;
        }

        await _emailNotifications.SendAsync(request.TenantId, key, submitter.Email, model, cancellationToken);
    }

    private async Task<IntegrationJob> CreateJobAsync(CustomerRequest request, CancellationToken cancellationToken)
    {
        var job = new IntegrationJob
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            InterfaceName = CustomerMaconomyMapper.InterfaceName, // "CustomerSync"
            Direction = IntegrationDirection.Outbound,
            SourceSystem = SystemName.Platform,
            TargetSystem = SystemName.Maconomy,
            Status = IntegrationJobStatus.Running,
            AttemptCount = request.SyncAttempts + 1,
            StartedAtUtc = DateTime.UtcNow,
        };
        await _jobs.AddAsync(job, cancellationToken);
        await LogAsync(job, "Information", $"Customer sync started for request {request.CustomerRequestNumber ?? request.Id.ToString()}.", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return job;
    }

    private async Task CompleteJobAsync(IntegrationJob job, IntegrationJobStatus status, string? error, string message, CancellationToken cancellationToken)
    {
        job.Status = status;
        job.ErrorMessage = error;
        job.CompletedAtUtc = DateTime.UtcNow;
        _jobs.Update(job);
        await LogAsync(job, status == IntegrationJobStatus.Completed ? "Information" : "Error", message, cancellationToken);
    }

    private Task LogAsync(IntegrationJob job, string level, string message, CancellationToken cancellationToken)
        => _logs.AddAsync(new IntegrationLog
        {
            TenantId = job.TenantId,
            JobId = job.Id,
            Level = level,
            Message = message,
        }, cancellationToken);

    private Task AppendAuditAsync(CustomerRequest request, CustomerAuditActionType action, string notes, CancellationToken cancellationToken)
        => _audit.AddAsync(new CustomerAuditEntry
        {
            Id = Guid.NewGuid(),
            CustomerRequestId = request.Id,
            TenantId = request.TenantId,
            ActionType = action,
            PerformedById = null,
            PerformedBy = "System",
            PerformedOnUtc = DateTime.UtcNow,
            Notes = notes,
        }, cancellationToken);
}
