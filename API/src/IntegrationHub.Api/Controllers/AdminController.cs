using IntegrationHub.Api.Integrations;
using IntegrationHub.Api.Security;
using IntegrationHub.Application.Abstractions.Auditing;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Retry;
using IntegrationHub.Domain.Entities;
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
    private readonly IUserRepository _users;
    private readonly ITenantRepository _tenants;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HealthCheckService _healthChecks;

    public AdminController(
        IIntegrationJobRepository jobs,
        IIntegrationLogRepository logs,
        IRetryQueueRepository retries,
        IRetryQueueManager retryManager,
        IUserRepository users,
        ITenantRepository tenants,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork,
        HealthCheckService healthChecks)
    {
        _jobs = jobs;
        _logs = logs;
        _retries = retries;
        _retryManager = retryManager;
        _users = users;
        _tenants = tenants;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _healthChecks = healthChecks;
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveActorNamesAsync(IEnumerable<Guid?> ids, CancellationToken cancellationToken)
        => await _users.GetFullNamesAsync(ids.Where(id => id.HasValue).Select(id => id!.Value), cancellationToken);

    private static string? NameOf(IReadOnlyDictionary<Guid, string> names, Guid? id)
        => id.HasValue && names.TryGetValue(id.Value, out var name) ? name : null;

    [HttpGet("jobs")]
    [RequirePermission(Permissions.JobsRead)]
    public async Task<IActionResult> GetJobs(
        [FromQuery] string? status, [FromQuery] string? interfaceName,
        [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate,
        [FromQuery] Guid? tenantId, [FromQuery] int page = 1, [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        (page, limit) = Normalize(page, limit);
        IntegrationJobStatus? statusFilter = Enum.TryParse<IntegrationJobStatus>(status, out var s) ? s : null;

        var (items, total) = await _jobs.QueryAsync(ResolveTenant(tenantId), statusFilter, interfaceName, fromDate, toDate, page, limit, cancellationToken);
        var names = await ResolveActorNamesAsync(items.SelectMany(j => new[] { j.CreatedById, j.UpdatedById }), cancellationToken);
        var tenantNames = (await _tenants.ListAsync(cancellationToken)).ToDictionary(t => t.Id, t => t.Name);
        var summaries = items.Select(j => new
        {
            jobId = j.Id, j.TenantId,
            tenantName = tenantNames.TryGetValue(j.TenantId, out var tname) ? tname : null,
            j.InterfaceName, status = j.Status.ToString(),
            direction = j.Direction.ToString(),
            sourceSystem = j.SourceSystem.ToString(), targetSystem = j.TargetSystem.ToString(),
            flowLabel = IntegrationFlows.Label(j.SourceSystem, j.TargetSystem, j.InterfaceName),
            createdDate = j.CreatedOnUtc, processedDate = j.CompletedAtUtc,
            createdBy = NameOf(names, j.CreatedById), updatedBy = NameOf(names, j.UpdatedById),
            createdOnUtc = j.CreatedOnUtc, updatedOnUtc = j.UpdatedOnUtc,
        });

        return Ok(ApiResponseFactory.Paginated(summaries, "Jobs retrieved.", page, limit, total));
    }

    [HttpGet("logs")]
    [RequirePermission(Permissions.LogsRead)]
    public async Task<IActionResult> GetLogs(
        [FromQuery] Guid? jobId, [FromQuery] string? status,
        [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate,
        [FromQuery] Guid? tenantId, [FromQuery] int page = 1, [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        (page, limit) = Normalize(page, limit);
        var (items, total) = await _logs.QueryAsync(ResolveTenant(tenantId), jobId, status, fromDate, toDate, page, limit, cancellationToken);
        var names = await ResolveActorNamesAsync(items.SelectMany(l => new[] { l.CreatedById, l.UpdatedById }), cancellationToken);
        var entries = items.Select(l => new
        {
            l.Id, jobId = l.JobId, level = l.Level, l.Message, createdDate = l.CreatedOnUtc,
            createdBy = NameOf(names, l.CreatedById), updatedBy = NameOf(names, l.UpdatedById), updatedOnUtc = l.UpdatedOnUtc,
        });
        return Ok(ApiResponseFactory.Paginated(entries, "Logs retrieved.", page, limit, total));
    }

    [HttpGet("retries")]
    [RequirePermission(Permissions.JobsRead)]
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
    [RequirePermission(Permissions.HealthRead)]
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
    [RequirePermission(Permissions.JobsRetry)]
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

    [HttpDelete("jobs/{jobId:guid}")]
    [RequirePermission(Permissions.JobsDelete)]
    public async Task<IActionResult> DeleteJob(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _jobs.GetByIdUnscopedAsync(jobId, cancellationToken);
        if (job is null)
        {
            return NotFound(ApiResponseFactory.Error(ApiErrorCodes.JobNotFound, "Job not found.", jobId.ToString()));
        }

        // Tenant Admins may only delete their own tenant's jobs; Super Admins may delete any.
        if (!User.IsSuperAdmin() && job.TenantId != User.GetActiveTenantId())
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponseFactory.Forbidden("Not permitted for this tenant."));
        }

        // In-flight jobs cannot be deleted (would orphan a running background execution).
        if (job.Status is IntegrationJobStatus.Created or IntegrationJobStatus.Running)
        {
            return StatusCode(StatusCodes.Status409Conflict,
                ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed, "A job that is queued or running cannot be deleted.", job.Status.ToString()));
        }

        _jobs.Remove(job);
        await _audit.AddAsync(nameof(IntegrationJob), job.Id.ToString(), "Deleted", details: job.InterfaceName, cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(new { message = "Job deleted." }, "Job deleted."));
    }

    /// <summary>Super Admins may target any tenant (or all); others are pinned to their active tenant.</summary>
    private Guid? ResolveTenant(Guid? requestedTenantId)
        => User.IsSuperAdmin() ? requestedTenantId : User.GetActiveTenantId();

    private static (int Page, int Limit) Normalize(int page, int limit)
        => (Math.Max(1, page), Math.Clamp(limit, 1, 100));
}
