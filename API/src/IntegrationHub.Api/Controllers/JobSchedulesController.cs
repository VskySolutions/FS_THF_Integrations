using IntegrationHub.Api.Models.Schedules;
using IntegrationHub.Api.Security;
using IntegrationHub.Application.Abstractions.Auditing;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Infrastructure.Jobs;
using IntegrationHub.Shared.Contracts;
using IntegrationHub.Shared.Security;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Api.Controllers;

/// <summary>
/// Manage the per-tenant cron schedules for the import flows. A Tenant Admin manages their own
/// tenant's schedules; a Super Admin manages any tenant's (via the <c>tenantId</c> query). The
/// platform-global retry job is not exposed here. The Worker's HangfireJobScheduler re-applies
/// changes within ~1 minute — no restart needed.
/// </summary>
[ApiController]
[Route("/api/admin/job-schedules")]
[RequirePermission(Permissions.JobsSchedule)]
[Produces("application/json")]
[Tags("Job Schedules")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
public sealed class JobSchedulesController : ControllerBase
{
    // The per-tenant import flows that can be scheduled, with a friendly label for the UI.
    private static readonly (string Name, string Label)[] KnownJobs =
    {
        (ExpenseImportJob.Name, "Expense import"),
        (InvoiceImportJob.Name, "Invoice import"),
        (VendorPaymentImportJob.Name, "Vendor payment import"),
    };

    private readonly IJobScheduleConfigurationRepository _schedules;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditTrailService _audit;

    public JobSchedulesController(IJobScheduleConfigurationRepository schedules, IUnitOfWork unitOfWork, IAuditTrailService audit)
    {
        _schedules = schedules;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    [HttpGet]
    [ProducesResponseType<ApiResponse<IEnumerable<JobScheduleResponse>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] Guid? tenantId, CancellationToken cancellationToken)
    {
        var (resolved, error) = ResolveTenant(tenantId);
        if (error is not null)
        {
            return error;
        }

        var result = new List<JobScheduleResponse>();
        foreach (var (name, label) in KnownJobs)
        {
            var row = await _schedules.GetAsync(name, resolved, cancellationToken);
            result.Add(new JobScheduleResponse(
                resolved, name, label,
                row?.CronExpression ?? string.Empty,
                row?.IsActive ?? false,
                Configured: row is not null,
                row?.UpdatedOnUtc));
        }

        return Ok(ApiResponseFactory.Success(result, "Schedules retrieved."));
    }

    [HttpPut("{jobName}")]
    public async Task<IActionResult> Update(string jobName, [FromQuery] Guid? tenantId, [FromBody] UpdateJobScheduleRequest request, CancellationToken cancellationToken)
    {
        if (!KnownJobs.Any(j => string.Equals(j.Name, jobName, StringComparison.Ordinal)))
        {
            return NotFound(ApiResponseFactory.NotFound($"Unknown job '{jobName}'."));
        }

        var (resolved, error) = ResolveTenant(tenantId);
        if (error is not null)
        {
            return error;
        }

        var cron = request.CronExpression?.Trim() ?? string.Empty;
        if (!IsValidCron(cron))
        {
            return BadRequest(ApiResponseFactory.Error(
                ApiErrorCodes.ValidationFailed, "Validation failed.",
                "Cron must be a standard 5-field expression, e.g. '0 2 * * *' (daily at 02:00 UTC)."));
        }

        var existing = await _schedules.GetAsync(jobName, resolved, cancellationToken);
        if (existing is null)
        {
            existing = new JobScheduleConfiguration
            {
                Id = Guid.NewGuid(),
                JobName = jobName,
                TenantId = resolved,
                CronExpression = cron,
                IsActive = request.IsActive,
                UpdatedDate = DateTime.UtcNow,
            };
            await _schedules.AddAsync(existing, cancellationToken);
        }
        else
        {
            existing.CronExpression = cron;
            existing.IsActive = request.IsActive;
            existing.UpdatedDate = DateTime.UtcNow;
            _schedules.Update(existing);
        }

        await _audit.AddAsync(nameof(JobScheduleConfiguration), $"{jobName}:{resolved}", "ScheduleUpdated",
            details: $"cron={cron}; active={request.IsActive}", cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var label = KnownJobs.First(j => j.Name == jobName).Label;
        return Ok(ApiResponseFactory.Success(
            new JobScheduleResponse(resolved, jobName, label, cron, existing.IsActive, true, existing.UpdatedOnUtc),
            "Schedule updated."));
    }

    /// <summary>Resolves the tenant whose schedules are being managed: a chosen tenant for Super Admins, the active tenant otherwise.</summary>
    private (Guid? TenantId, IActionResult? Error) ResolveTenant(Guid? requested)
    {
        if (User.IsSuperAdmin())
        {
            var tid = requested ?? User.GetActiveTenantId();
            return tid is null
                ? (null, BadRequest(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed, "Validation failed.", "tenantId is required.")))
                : (tid, null);
        }

        var active = User.GetActiveTenantId();
        return active is null
            ? (null, StatusCode(StatusCodes.Status403Forbidden, ApiResponseFactory.Forbidden("No active tenant for the caller.")))
            : (active, null);
    }

    /// <summary>Lightweight validation: a standard 5-field cron expression. Hangfire validates fully at registration.</summary>
    private static bool IsValidCron(string cron)
        => !string.IsNullOrWhiteSpace(cron)
            && cron.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length == 5;
}
