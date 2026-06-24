using System.Text.Json;
using IntegrationHub.Api.Dashboard;
using IntegrationHub.Api.Models.Dashboard;
using IntegrationHub.Api.Security;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Shared.Contracts;
using IntegrationHub.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Api.Controllers;

/// <summary>
/// Read-only dashboard aggregations (WO-72). Each section is permission-gated and tenant-scoped:
/// Tenant Admins see their own tenant; a Super Admin may target any tenant via <c>?tenantId=</c>.
/// The platform section is Super-Admin only and short-lived cached. Layout endpoints persist a
/// per-user widget arrangement, defaulting to a role-based layout when none is saved.
/// </summary>
[ApiController]
[Authorize]
[Route("/api/dashboard")]
[Produces("application/json")]
[Tags("Dashboard")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status500InternalServerError)]
public sealed class DashboardController : ControllerBase
{
    private readonly IDashboardQueryService _query;
    private readonly IDashboardCacheService _cache;
    private readonly IDashboardLayoutRepository _layouts;
    private readonly IUnitOfWork _unitOfWork;

    public DashboardController(
        IDashboardQueryService query,
        IDashboardCacheService cache,
        IDashboardLayoutRepository layouts,
        IUnitOfWork unitOfWork)
    {
        _query = query;
        _cache = cache;
        _layouts = layouts;
        _unitOfWork = unitOfWork;
    }

    // ---- Jobs ----

    [HttpGet("jobs")]
    [RequirePermission(Permissions.JobsRead)]
    [ProducesResponseType<ApiResponse<JobDashboardDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Jobs([FromQuery] string dateRange = "7d", [FromQuery] Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var data = await _query.GetJobsAsync(ResolveScope(tenantId), dateRange, cancellationToken);
        return Ok(ApiResponseFactory.Success(data, "Job dashboard retrieved."));
    }

    // ---- Health ----

    [HttpGet("health")]
    [RequirePermission(Permissions.HealthRead)]
    [ProducesResponseType<ApiResponse<HealthDashboardDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Health(CancellationToken cancellationToken)
    {
        var data = await _query.GetHealthAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(data, "Health dashboard retrieved."));
    }

    // ---- Customers ----

    [HttpGet("customers")]
    [RequirePermission(Permissions.CustomersReview)]
    [ProducesResponseType<ApiResponse<CustomerDashboardDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Customers([FromQuery] string dateRange = "7d", [FromQuery] Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var data = await _query.GetCustomersAsync(ResolveScope(tenantId), dateRange, cancellationToken);
        return Ok(ApiResponseFactory.Success(data, "Customer dashboard retrieved."));
    }

    // ---- Users ----

    [HttpGet("users")]
    [RequirePermission(Permissions.UsersRead)]
    [ProducesResponseType<ApiResponse<UserDashboardDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Users([FromQuery] string dateRange = "7d", [FromQuery] Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var data = await _query.GetUsersAsync(ResolveScope(tenantId), dateRange, cancellationToken);
        return Ok(ApiResponseFactory.Success(data, "User dashboard retrieved."));
    }

    // ---- Platform (Super Admin only) ----

    [HttpGet("platform")]
    [ProducesResponseType<ApiResponse<PlatformDashboardDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Platform([FromQuery] string dateRange = "7d", CancellationToken cancellationToken = default)
    {
        if (!User.IsSuperAdmin())
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponseFactory.Forbidden("Platform dashboard is restricted to Super Admins."));
        }

        var forceRefresh = TruthyHeader(Request.Headers["X-Dashboard-Force-Refresh"]);
        var data = await _cache.GetOrAddAsync(
            $"dashboard:platform:{dateRange}",
            forceRefresh,
            () => _query.GetPlatformAsync(dateRange, forceRefresh, cancellationToken));

        return Ok(ApiResponseFactory.Success(data, "Platform dashboard retrieved."));
    }

    // ---- Layout ----

    [HttpGet("layout")]
    [ProducesResponseType<ApiResponse<DashboardLayoutResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLayout(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is not { } uid)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, ApiResponseFactory.Unauthorized("No user in token."));
        }

        var saved = await _layouts.GetByUserAsync(uid, cancellationToken);
        if (saved is not null)
        {
            return Ok(ApiResponseFactory.Success(
                new DashboardLayoutResponse(saved.WidgetOrder, saved.HiddenWidgets, saved.CollapsedWidgets),
                "Layout retrieved."));
        }

        var role = ResolveDashboardRole();
        return Ok(ApiResponseFactory.Success(
            new DashboardLayoutResponse(
                DashboardDefaultLayouts.For(role),
                DashboardDefaultLayouts.DefaultHiddenFor(role),
                Array.Empty<string>()),
            "Default layout retrieved."));
    }

    [HttpPut("layout")]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveLayout([FromBody] DashboardLayoutRequest body, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is not { } uid)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, ApiResponseFactory.Unauthorized("No user in token."));
        }

        var layout = new DashboardLayout
        {
            Id = Guid.NewGuid(),
            UserId = uid,
            WidgetOrderJson = JsonSerializer.Serialize(body.WidgetOrder ?? new List<string>()),
            HiddenWidgetsJson = JsonSerializer.Serialize(body.HiddenWidgets ?? new List<string>()),
            CollapsedWidgetsJson = JsonSerializer.Serialize(body.CollapsedWidgets ?? new List<string>()),
        };
        await _layouts.UpsertAsync(layout, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponseFactory.Success(new { saved = true }, "Layout saved."));
    }

    // ---- Helpers ----

    /// <summary>Super Admins may target any tenant (or pass null for the cross-tenant view); others are pinned to their active tenant.</summary>
    private Guid? ResolveScope(Guid? requestedTenantId)
        => User.IsSuperAdmin() ? requestedTenantId : User.GetActiveTenantId();

    /// <summary>Resolves the layout tier: Super Admin, else Tenant Admin (users.read + tenants.read), else Common.</summary>
    private DashboardRole ResolveDashboardRole()
    {
        if (User.IsSuperAdmin())
        {
            return DashboardRole.SuperAdmin;
        }
        if (User.HasPermission(Permissions.UsersRead) && User.HasPermission(Permissions.TenantsRead))
        {
            return DashboardRole.TenantAdmin;
        }
        return DashboardRole.Common;
    }

    private static bool TruthyHeader(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(value, "0", StringComparison.Ordinal);
}
