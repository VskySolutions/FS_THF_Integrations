using IntegrationHub.Api.Security;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Retry;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Shared.Contracts;
using IntegrationHub.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IntegrationHub.Api.Controllers;

/// <summary>
/// Integration administration (WO-29). All endpoints require TenantAdminOrAbove. Results
/// are tenant-scoped: Tenant Admins see only their tenant; Super Admins see all tenants
/// unless a <c>tenantId</c> filter is supplied (REQ-INF-008, REQ-TNT-007).
/// </summary>
[ApiController]
[Route("/api/admin")]
[Authorize(Policy = AuthorizationPolicies.TenantAdminOrAbove)]
[Produces("application/json")]
[Tags("Admin")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status500InternalServerError)]
public sealed class AdminController : ControllerBase
{
    private readonly IIntegrationJobRepository _jobs;
    private readonly IIntegrationLogRepository _logs;
    private readonly IRetryQueueRepository _retries;
    private readonly IRetryQueueManager _retryManager;
    private readonly HealthCheckService _healthChecks;

    public AdminController(
        IIntegrationJobRepository jobs,
        IIntegrationLogRepository logs,
        IRetryQueueRepository retries,
        IRetryQueueManager retryManager,
        HealthCheckService healthChecks)
    {
        _jobs = jobs;
        _logs = logs;
        _retries = retries;
        _retryManager = retryManager;
        _healthChecks = healthChecks;
    }

    [HttpGet("jobs")]
    public async Task<IActionResult> GetJobs(
        [FromQuery] string? status, [FromQuery] string? interfaceName,
        [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate,
        [FromQuery] Guid? tenantId, [FromQuery] int page = 1, [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        (page, limit) = Normalize(page, limit);
        IntegrationJobStatus? statusFilter = Enum.TryParse<IntegrationJobStatus>(status, out var s) ? s : null;

        var (items, total) = await _jobs.QueryAsync(ResolveTenant(tenantId), statusFilter, interfaceName, fromDate, toDate, page, limit, cancellationToken);
        var summaries = items.Select(j => new
        {
            jobId = j.Id, j.TenantId, j.InterfaceName, status = j.Status.ToString(),
            sourceSystem = j.SourceSystem.ToString(), targetSystem = j.TargetSystem.ToString(),
            createdDate = j.CreatedAtUtc, processedDate = j.CompletedAtUtc,
        });

        return Ok(ApiResponseFactory.Paginated(summaries, "Jobs retrieved.", page, limit, total));
    }

    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs(
        [FromQuery] Guid? jobId, [FromQuery] string? status,
        [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate,
        [FromQuery] Guid? tenantId, [FromQuery] int page = 1, [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        (page, limit) = Normalize(page, limit);
        var (items, total) = await _logs.QueryAsync(ResolveTenant(tenantId), jobId, status, fromDate, toDate, page, limit, cancellationToken);
        var entries = items.Select(l => new { l.Id, jobId = l.JobId, level = l.Level, l.Message, createdDate = l.CreatedAtUtc });
        return Ok(ApiResponseFactory.Paginated(entries, "Logs retrieved.", page, limit, total));
    }

    [HttpGet("retries")]
    public async Task<IActionResult> GetRetries(
        [FromQuery] Guid? tenantId, [FromQuery] int page = 1, [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        (page, limit) = Normalize(page, limit);
        var (items, total) = await _retries.QueryAsync(ResolveTenant(tenantId), page, limit, cancellationToken);
        var entries = items.Select(r => new { jobId = r.JobId, retryCount = r.RetryCount, nextRetryDate = r.NextRetryDate, status = r.Status.ToString() });
        return Ok(ApiResponseFactory.Paginated(entries, "Retry queue retrieved.", page, limit, total));
    }

    [HttpGet("health")]
    public async Task<IActionResult> GetHealth(CancellationToken cancellationToken)
    {
        var report = await _healthChecks.CheckHealthAsync(cancellationToken);
        var summary = new
        {
            status = report.Status.ToString(),
            components = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString() }),
        };
        return Ok(ApiResponseFactory.Success(summary, "Health retrieved."));
    }

    [HttpPost("retry/{jobId:guid}")]
    public async Task<IActionResult> ManualRetry(Guid jobId, CancellationToken cancellationToken)
    {
        var actor = User.GetUserId()?.ToString() ?? "system";
        var retried = await _retryManager.ManualRetryAsync(jobId, actor, cancellationToken);
        if (!retried)
        {
            return NotFound(ApiResponseFactory.Error(ApiErrorCodes.JobNotFound, "Job not found or not in a failed state.", jobId.ToString()));
        }

        return Ok(ApiResponseFactory.Success(new { jobId, status = "Enqueued" }, "Job re-enqueued."));
    }

    /// <summary>Super Admins may target any tenant (or all); others are pinned to their active tenant.</summary>
    private Guid? ResolveTenant(Guid? requestedTenantId)
        => User.IsSuperAdmin() ? requestedTenantId : User.GetActiveTenantId();

    private static (int Page, int Limit) Normalize(int page, int limit)
        => (Math.Max(1, page), Math.Clamp(limit, 1, 100));
}
