using global::Hangfire;
using IntegrationHub.Api.Security;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Infrastructure.Jobs;
using IntegrationHub.Shared.Contracts;
using IntegrationHub.Shared.Security;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Api.Controllers;

/// <summary>
/// Concur import trigger endpoints (WO-24). Each creates an <see cref="IntegrationJob"/>
/// for the active tenant, enqueues the corresponding Hangfire job (which carries the
/// tenant in its payload), and returns HTTP 202 with the job id (Concur ADR-002).
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackgroundJobClient _backgroundJobs;

    public ConcurController(IIntegrationJobRepository jobs, IUnitOfWork unitOfWork, IBackgroundJobClient backgroundJobs)
    {
        _jobs = jobs;
        _unitOfWork = unitOfWork;
        _backgroundJobs = backgroundJobs;
    }

    [HttpPost("expenses/import")]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ImportExpenses(CancellationToken cancellationToken)
    {
        var jobId = await CreateJobAsync("ExpenseImport", cancellationToken);
        _backgroundJobs.Enqueue<ExpenseImportJob>(job => job.RunForJobAsync(jobId, CancellationToken.None));
        return Accepted(ApiResponseFactory.Success(new { jobId }, "Expense import enqueued."));
    }

    [HttpPost("invoices/import")]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ImportInvoices(CancellationToken cancellationToken)
    {
        var jobId = await CreateJobAsync("InvoiceImport", cancellationToken);
        _backgroundJobs.Enqueue<InvoiceImportJob>(job => job.RunForJobAsync(jobId, CancellationToken.None));
        return Accepted(ApiResponseFactory.Success(new { jobId }, "Invoice import enqueued."));
    }

    [HttpPost("payments/import")]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ImportPayments(CancellationToken cancellationToken)
    {
        var jobId = await CreateJobAsync("VendorPaymentImport", cancellationToken);
        _backgroundJobs.Enqueue<VendorPaymentImportJob>(job => job.RunForJobAsync(jobId, CancellationToken.None));
        return Accepted(ApiResponseFactory.Success(new { jobId }, "Payment import enqueued."));
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
            CreatedAtUtc = DateTime.UtcNow,
        };
        await _jobs.AddAsync(job, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken); // TenantId stamped from ITenantContext
        return job.Id;
    }
}
