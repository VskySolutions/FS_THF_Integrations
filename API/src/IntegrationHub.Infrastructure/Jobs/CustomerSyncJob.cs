using global::Hangfire;
using IntegrationHub.Application.Abstractions.Connectors.Maconomy;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Tenancy;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace IntegrationHub.Infrastructure.Jobs;

/// <summary>
/// On-demand Hangfire job that synchronises an approved <see cref="CustomerRequest"/> to Maconomy
/// using the existing <see cref="IMaconomyConnector"/>. Triggered by the approval service on final
/// approval (and by a manual Retry Sync). Reconstructs the tenant context from the payload, resolves
/// the tenant's Maconomy credentials (immediate non-retriable failure if absent), pushes the customer
/// master record, and records the outcome. Transient failures are retried with incremental backoff.
/// </summary>
public sealed class CustomerSyncJob
{
    /// <summary>Incremental backoff (minutes) applied to transient sync failures, by attempt number.</summary>
    private static readonly int[] BackoffMinutes = { 5, 15, 30, 60 };

    private readonly ITenantContext _tenantContext;
    private readonly ITenantRepository _tenants;
    private readonly ICustomerRequestRepository _requests;
    private readonly ICustomerAuditRepository _audit;
    private readonly ITenantApiConfigurationService _configurationService;
    private readonly IMaconomyConnector _maconomy;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ILogger<CustomerSyncJob> _logger;

    public CustomerSyncJob(
        ITenantContext tenantContext,
        ITenantRepository tenants,
        ICustomerRequestRepository requests,
        ICustomerAuditRepository audit,
        ITenantApiConfigurationService configurationService,
        IMaconomyConnector maconomy,
        IUnitOfWork unitOfWork,
        IBackgroundJobClient backgroundJobs,
        ILogger<CustomerSyncJob> logger)
    {
        _tenantContext = tenantContext;
        _tenants = tenants;
        _requests = requests;
        _audit = audit;
        _configurationService = configurationService;
        _maconomy = maconomy;
        _unitOfWork = unitOfWork;
        _backgroundJobs = backgroundJobs;
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

        await AppendAuditAsync(request, CustomerAuditActionType.SyncStarted, "Maconomy synchronisation started.", cancellationToken);

        // REQ-CUS-016.2: missing credentials → immediate, non-retriable failure.
        var config = await _configurationService.GetMaconomyConfigAsync(cancellationToken);
        if (config is null)
        {
            await FailAsync(request, "Maconomy credentials not configured for tenant", retriable: false, cancellationToken);
            return;
        }

        request.SyncAttempts++;
        var result = await _maconomy.CreateCustomerAsync(BuildPayload(request), cancellationToken);

        if (result.Success)
        {
            request.MaconomyCustomerNumber = result.Payload!.EntityId;
            request.Status = CustomerRequestStatus.Synced;
            request.LastSyncError = null;
            await AppendAuditAsync(request, CustomerAuditActionType.Synced,
                $"Synced to Maconomy as customer {request.MaconomyCustomerNumber}.", cancellationToken);
            _requests.Update(request);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("CustomerSyncJob: request {Request} synced as {Number}", request.Id, request.MaconomyCustomerNumber);
            return;
        }

        // Transient failure with retries remaining → reschedule with backoff; otherwise mark Failed.
        if (result.IsRetriable && request.SyncAttempts < BackoffMinutes.Length)
        {
            var delay = TimeSpan.FromMinutes(BackoffMinutes[request.SyncAttempts]);
            request.LastSyncError = result.ErrorMessage;
            await AppendAuditAsync(request, CustomerAuditActionType.SyncFailed,
                $"Attempt {request.SyncAttempts} failed: {result.ErrorMessage}. Retrying in {delay.TotalMinutes:N0} min.", cancellationToken);
            _requests.Update(request);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _backgroundJobs.Schedule<CustomerSyncJob>(job => job.RunAsync(request.Id, tenantId, CancellationToken.None), delay);
            return;
        }

        await FailAsync(request, result.ErrorMessage ?? "Maconomy sync failed.", retriable: result.IsRetriable, cancellationToken);
    }

    private async Task FailAsync(CustomerRequest request, string error, bool retriable, CancellationToken cancellationToken)
    {
        request.Status = CustomerRequestStatus.Failed;
        request.LastSyncError = error;
        await AppendAuditAsync(request, CustomerAuditActionType.SyncFailed,
            retriable ? $"Sync failed after {request.SyncAttempts} attempt(s): {error}" : error, cancellationToken);
        _requests.Update(request);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        // Tenant Admin notification is surfaced via the audit trail + structured log (no email channel in MVP).
        _logger.LogError("CustomerSyncJob: request {Request} for tenant {Tenant} marked Failed: {Error}", request.Id, request.TenantId, error);
    }

    private static MaconomyCustomer BuildPayload(CustomerRequest r) => new(
        Name: r.CompanyName,
        LegalName: r.LegalName,
        ContactPerson: r.ContactPerson,
        Email: r.EmailAddress,
        Phone: r.PhoneNumber,
        Website: r.Website,
        Country: r.Country,
        State: r.StateProvince,
        City: r.City,
        AddressLine1: r.AddressLine1,
        AddressLine2: r.AddressLine2,
        PostalCode: r.PostalCode,
        TaxNumber: r.TaxNumber,
        RegistrationNumber: r.RegistrationNumber,
        BusinessUnit: r.BusinessUnit,
        Currency: r.Currency,
        CustomerGroup: r.CustomerGroup,
        PaymentTerms: r.PaymentTerms,
        CreditLimit: r.CreditLimit,
        Industry: r.Industry,
        InvoiceLanguage: r.InvoiceLanguage,
        BillingEmail: r.BillingEmail);

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
