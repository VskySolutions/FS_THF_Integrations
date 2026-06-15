using global::Hangfire;
using IntegrationHub.Api.Integrations;
using IntegrationHub.Api.Security;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Tenancy;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Infrastructure.Jobs;
using IntegrationHub.Shared.Contracts;
using IntegrationHub.Shared.Security;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Api.Controllers;

/// <summary>
/// Concur import trigger endpoints (WO-24). Each creates an <see cref="IntegrationJob"/>
/// for the active tenant, enqueues the corresponding Hangfire job (which carries the tenant
/// in its payload), and returns HTTP 202 with the job id. The flow's field mappings are
/// resolved at run time from the tenant's (source, destination) + flow rules.
/// </summary>
[ApiController]
[Route("/api/concur")]
[RequirePermission(Permissions.JobsTrigger)]
[Produces("application/json")]
[Tags("Concur")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status500InternalServerError)]
public sealed class ConcurController : ControllerBase
{
    private readonly IIntegrationJobRepository _jobs;
    private readonly ITenantRepository _tenants;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackgroundJobClient _backgroundJobs;

    public ConcurController(
        IIntegrationJobRepository jobs,
        ITenantRepository tenants,
        ITenantContext tenantContext,
        IUnitOfWork unitOfWork,
        IBackgroundJobClient backgroundJobs)
    {
        _jobs = jobs;
        _tenants = tenants;
        _tenantContext = tenantContext;
        _unitOfWork = unitOfWork;
        _backgroundJobs = backgroundJobs;
    }

    [HttpPost("expenses/import")]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ImportExpenses([FromQuery] Guid? tenantId, CancellationToken cancellationToken)
    {
        var guard = await ResolveTenantAsync(tenantId, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }
        var jobId = await CreateJobAsync(IntegrationFlows.ExpenseImport, cancellationToken);
        _backgroundJobs.Enqueue<ExpenseImportJob>(job => job.RunForJobAsync(jobId, CancellationToken.None));
        return Accepted(ApiResponseFactory.Success(new { jobId }, "Expense import enqueued."));
    }

    [HttpPost("invoices/import")]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ImportInvoices([FromQuery] Guid? tenantId, CancellationToken cancellationToken)
    {
        var guard = await ResolveTenantAsync(tenantId, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }
        var jobId = await CreateJobAsync(IntegrationFlows.InvoiceImport, cancellationToken);
        _backgroundJobs.Enqueue<InvoiceImportJob>(job => job.RunForJobAsync(jobId, CancellationToken.None));
        return Accepted(ApiResponseFactory.Success(new { jobId }, "Invoice import enqueued."));
    }

    [HttpPost("payments/import")]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ImportPayments([FromQuery] Guid? tenantId, CancellationToken cancellationToken)
    {
        var guard = await ResolveTenantAsync(tenantId, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }
        var jobId = await CreateJobAsync(IntegrationFlows.VendorPaymentImport, cancellationToken);
        _backgroundJobs.Enqueue<VendorPaymentImportJob>(job => job.RunForJobAsync(jobId, CancellationToken.None));
        return Accepted(ApiResponseFactory.Success(new { jobId }, "Payment import enqueued."));
    }

    /// <summary>
    /// Re-points the request's tenant context to <paramref name="tenantId"/> when a Super Admin
    /// targets another tenant (so the job and its background snapshot are scoped to it). Tenant
    /// Admins may only target their own tenant; an omitted id uses the caller's active tenant.
    /// </summary>
    private async Task<IActionResult?> ResolveTenantAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        if (tenantId is not { } target || target == _tenantContext.TenantId)
        {
            return null; // no override needed — use the active tenant resolved by the middleware
        }

        if (!User.IsSuperAdmin())
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponseFactory.Forbidden("Not permitted for this tenant."));
        }

        var tenant = await _tenants.GetByIdAsync(target, cancellationToken);
        if (tenant is null)
        {
            return NotFound(ApiResponseFactory.Error(ApiErrorCodes.TenantNotFound, "Tenant not found.", target.ToString()));
        }
        if (tenant.Status != TenantStatus.Active)
        {
            return BadRequest(ApiResponseFactory.Error(ApiErrorCodes.TenantInactive, "The tenant is not active.", target.ToString()));
        }

        _tenantContext.Set(tenant.Id, tenant.Identifier);
        return null;
    }

    private async Task<Guid> CreateJobAsync(string interfaceName, CancellationToken cancellationToken)
    {
        var job = new IntegrationJob
        {
            Id = Guid.NewGuid(),
            InterfaceName = interfaceName,
            Direction = IntegrationDirection.Inbound,
            SourceSystem = SystemName.Concur,
            TargetSystem = SystemName.Maconomy,
            Status = IntegrationJobStatus.Created,
        };
        await _jobs.AddAsync(job, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken); // TenantId stamped from ITenantContext
        return job.Id;
    }
}
