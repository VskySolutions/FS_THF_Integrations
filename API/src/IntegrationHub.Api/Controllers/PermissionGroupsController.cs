using IntegrationHub.Api.Models.PermissionGroups;
using IntegrationHub.Api.Security;
using IntegrationHub.Application.Abstractions.Auditing;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Security;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Shared.Contracts;
using IntegrationHub.Shared.Security;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Api.Controllers;

/// <summary>
/// Permission Group management (RBAC composition layer: Permission Keys → Permission Groups →
/// Roles → Users). Tenant-scoped with a Super Admin <c>?tenantId=</c> override. All mutations
/// require <c>groups.manage</c>; Tenant Admins may only assign keys within their tenant's ceiling.
/// </summary>
[ApiController]
[Route("/api/admin/permission-groups")]
[RequirePermission(Permissions.GroupsManage)]
[Produces("application/json")]
[Tags("Permission Groups")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status500InternalServerError)]
public sealed class PermissionGroupsController : ControllerBase
{
    private readonly IPermissionGroupRepository _groups;
    private readonly IRoleRepository _roles;
    private readonly ITenantRepository _tenants;
    private readonly IPermissionGroupEffectivePermissionService _effective;
    private readonly IAuditTrailService _audit;
    private readonly IAuditTrailRepository _auditRead;
    private readonly IUnitOfWork _unitOfWork;

    public PermissionGroupsController(
        IPermissionGroupRepository groups,
        IRoleRepository roles,
        ITenantRepository tenants,
        IPermissionGroupEffectivePermissionService effective,
        IAuditTrailService audit,
        IAuditTrailRepository auditRead,
        IUnitOfWork unitOfWork)
    {
        _groups = groups;
        _roles = roles;
        _tenants = tenants;
        _effective = effective;
        _audit = audit;
        _auditRead = auditRead;
        _unitOfWork = unitOfWork;
    }

    // ---- Templates (declared before {id} routes so "templates" isn't captured as an id) ----

    [HttpGet("templates")]
    public async Task<IActionResult> Templates(CancellationToken cancellationToken)
    {
        var templates = await _groups.GetTemplatesAsync(cancellationToken);
        var data = templates.Select(t => new PermissionGroupTemplateResponse(t.Id, t.Name, t.Description, t.PermissionKeys, t.IsSeeded));
        return Ok(ApiResponseFactory.Success(data, "Templates retrieved."));
    }

    [HttpPost("templates")]
    public async Task<IActionResult> CreateTemplate([FromBody] SaveTemplateRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsSuperAdmin())
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponseFactory.Forbidden("Only a Super Admin may manage templates."));
        }
        var keys = NormalizeKnown(request.PermissionKeys, out var unknown);
        if (unknown.Count > 0)
        {
            return BadRequest(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed, "Unknown permission key(s).", string.Join(", ", unknown)));
        }

        var template = new PermissionGroupTemplate
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IsSeeded = false,
            PermissionKeysJson = System.Text.Json.JsonSerializer.Serialize(keys),
        };
        await _groups.AddTemplateAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(
            new PermissionGroupTemplateResponse(template.Id, template.Name, template.Description, keys, template.IsSeeded), "Template created."));
    }

    // ---- List ----

    [HttpGet]
    [ProducesResponseType<ApiResponse<IEnumerable<PermissionGroupSummaryResponse>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? tenantId,
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] bool? usedByRoles,
        [FromQuery] string? category,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        Guid? scopeTenant = User.IsSuperAdmin() && tenantId is { } tid ? tid : null;

        var (items, total) = await _groups.ListAsync(
            scopeTenant, search, isActive, usedByRoles, category, Math.Max(1, page), Math.Clamp(limit, 1, 100), cancellationToken);

        var data = new List<PermissionGroupSummaryResponse>(items.Count);
        foreach (var g in items)
        {
            var rolesUsing = await _groups.CountRolesUsingGroupAsync(g.Id, cancellationToken);
            data.Add(ToSummary(g, rolesUsing));
        }

        return Ok(ApiResponseFactory.Paginated(data, "Permission groups retrieved.", page, limit, total));
    }

    // ---- Detail ----

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApiResponse<PermissionGroupDetailResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var group = await LoadAsync(id, cancellationToken);
        if (group is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Permission group not found."));
        }

        var rolesUsing = await _groups.GetRolesUsingGroupAsync(id, cancellationToken);
        var audit = await _auditRead.ListByEntityAsync(nameof(PermissionGroup), id.ToString(), 100, cancellationToken);
        return Ok(ApiResponseFactory.Success(ToDetail(group, rolesUsing, audit), "Permission group retrieved."));
    }

    // ---- Create ----

    [HttpPost]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateGroupRequest body, CancellationToken cancellationToken)
    {
        var (tenantId, tenantError) = await ResolveTargetTenantAsync(body.TenantId, cancellationToken);
        if (tenantError is not null)
        {
            return tenantError;
        }

        var keys = NormalizeKnown(body.PermissionKeys, out var unknown);
        if (unknown.Count > 0)
        {
            return BadRequest(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed, "Unknown permission key(s).", string.Join(", ", unknown)));
        }
        var ceiling = await CheckCeilingAsync(tenantId, keys, cancellationToken);
        if (ceiling is not null)
        {
            return ceiling;
        }
        if (await _groups.NameExistsAsync(tenantId, body.Name.Trim(), Guid.Empty, cancellationToken))
        {
            return Conflict(ApiResponseFactory.Error(ApiErrorCodes.DuplicateGroupName, "A group with this name already exists.", body.Name.Trim()));
        }

        var group = new PermissionGroup
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = body.Name.Trim(),
            Description = body.Description?.Trim(),
            IsActive = true,
        };
        await _groups.AddAsync(group, cancellationToken);
        foreach (var key in keys)
        {
            await _groups.AddPermissionAsync(new PermissionGroupPermission { Id = Guid.NewGuid(), PermissionGroupId = group.Id, PermissionKey = key }, cancellationToken);
        }
        await _audit.AddAsync(nameof(PermissionGroup), group.Id.ToString(), "Created",
            details: $"name={group.Name}; keys={keys.Count}", cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponseFactory.Success(new { groupId = group.Id }, "Permission group created."));
    }

    // ---- Update ----

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateGroupRequest body, CancellationToken cancellationToken)
    {
        var group = await LoadAsync(id, cancellationToken);
        if (group is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Permission group not found."));
        }

        var keys = NormalizeKnown(body.PermissionKeys, out var unknown);
        if (unknown.Count > 0)
        {
            return BadRequest(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed, "Unknown permission key(s).", string.Join(", ", unknown)));
        }
        var ceiling = await CheckCeilingAsync(group.TenantId, keys, cancellationToken);
        if (ceiling is not null)
        {
            return ceiling;
        }
        if (!string.Equals(group.Name, body.Name.Trim(), StringComparison.OrdinalIgnoreCase)
            && await _groups.NameExistsAsync(group.TenantId, body.Name.Trim(), group.Id, cancellationToken))
        {
            return Conflict(ApiResponseFactory.Error(ApiErrorCodes.DuplicateGroupName, "A group with this name already exists.", body.Name.Trim()));
        }

        var before = group.Permissions.Select(p => p.PermissionKey).OrderBy(k => k).ToList();
        group.Name = body.Name.Trim();
        group.Description = body.Description?.Trim();
        _groups.RemovePermissions(group.Permissions.ToList());
        foreach (var key in keys)
        {
            await _groups.AddPermissionAsync(new PermissionGroupPermission { Id = Guid.NewGuid(), PermissionGroupId = group.Id, PermissionKey = key }, cancellationToken);
        }
        _groups.Update(group);
        await _audit.AddAsync(nameof(PermissionGroup), group.Id.ToString(), "Updated",
            details: $"keys before=[{string.Join(",", before)}] after=[{string.Join(",", keys)}]", cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Propagate the new permission set to every role composed from this group.
        await _effective.RecomputeForGroupAsync(group.Id, cancellationToken);

        return Ok(ApiResponseFactory.Success(new { groupId = group.Id }, "Permission group updated."));
    }

    // ---- Activate / Deactivate ----

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] SetGroupStatusRequest body, CancellationToken cancellationToken)
    {
        var group = await LoadAsync(id, cancellationToken);
        if (group is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Permission group not found."));
        }

        group.IsActive = body.IsActive;
        _groups.Update(group);
        await _audit.AddAsync(nameof(PermissionGroup), group.Id.ToString(), "StatusChanged",
            details: body.IsActive ? "Activated" : "Deactivated", cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // A deactivated group contributes zero; reactivation restores its keys.
        await _effective.RecomputeForGroupAsync(group.Id, cancellationToken);

        return Ok(ApiResponseFactory.Success(new { groupId = group.Id, isActive = group.IsActive },
            body.IsActive ? "Permission group activated." : "Permission group deactivated."));
    }

    // ---- Delete ----

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var group = await LoadAsync(id, cancellationToken);
        if (group is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Permission group not found."));
        }

        // Capture affected roles, then remove all composition links and soft-delete the group (REQ-PG-009.3).
        var affectedRoles = (await _groups.GetRolesUsingGroupAsync(id, cancellationToken)).Select(r => r.RoleId).ToList();
        foreach (var link in await _groups.GetRoleLinksAsync(id, cancellationToken))
        {
            _groups.RemoveRoleLink(link);
        }
        _groups.RemovePermissions(group.Permissions.ToList());
        _groups.Remove(group);
        await _audit.AddAsync(nameof(PermissionGroup), group.Id.ToString(), "Deleted", details: group.Name, cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var roleId in affectedRoles)
        {
            await _effective.RecomputeForRoleAsync(roleId, cancellationToken);
            await _audit.AddAsync(nameof(Role), roleId.ToString(), "PermissionGroupRemoved", details: $"group {group.Name} deleted", cancellationToken: cancellationToken);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponseFactory.Success(new { groupId = id }, "Permission group deleted."));
    }

    // ---- Helpers ----

    private Task<PermissionGroup?> LoadAsync(Guid id, CancellationToken cancellationToken)
        => User.IsSuperAdmin()
            ? _groups.GetByIdUnscopedAsync(id, cancellationToken)
            : _groups.GetByIdAsync(id, cancellationToken);

    private async Task<(Guid TenantId, IActionResult? Error)> ResolveTargetTenantAsync(Guid? requested, CancellationToken cancellationToken)
    {
        var active = User.GetActiveTenantId();
        if (User.IsSuperAdmin() && requested is { } target && target != active)
        {
            var tenant = await _tenants.GetByIdAsync(target, cancellationToken);
            if (tenant is null)
            {
                return (Guid.Empty, NotFound(ApiResponseFactory.Error(ApiErrorCodes.TenantNotFound, "Tenant not found.", target.ToString())));
            }
            if (tenant.Status != TenantStatus.Active)
            {
                return (Guid.Empty, BadRequest(ApiResponseFactory.Error(ApiErrorCodes.TenantInactive, "The tenant is not active.", target.ToString())));
            }
            return (target, null);
        }

        return active is { } a
            ? (a, null)
            : (Guid.Empty, StatusCode(StatusCodes.Status403Forbidden, ApiResponseFactory.Forbidden("No active tenant for the caller.")));
    }

    /// <summary>Normalises to known catalogue keys (distinct) and reports any unknown ones.</summary>
    private static List<string> NormalizeKnown(IEnumerable<string> keys, out List<string> unknown)
    {
        var distinct = keys.Select(k => k?.Trim() ?? string.Empty).Where(k => k.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        unknown = distinct.Where(k => !Permissions.All.Contains(k)).ToList();
        return distinct.Where(Permissions.All.Contains).ToList();
    }

    /// <summary>Enforces the Tenant Admin ceiling (ADR-003). Returns a 403 result when keys escape it; null when allowed.</summary>
    private async Task<IActionResult?> CheckCeilingAsync(Guid tenantId, IReadOnlyList<string> keys, CancellationToken cancellationToken)
    {
        if (User.IsSuperAdmin())
        {
            return null;
        }

        var ceiling = new HashSet<string>(Permissions.ForTenantAdmin(), StringComparer.Ordinal);
        foreach (var role in await _roles.ListByTenantAsync(tenantId, cancellationToken))
        {
            ceiling.UnionWith(role.Permissions);
            ceiling.UnionWith(role.EffectivePermissions);
        }

        var disallowed = keys.Where(k => !ceiling.Contains(k)).ToList();
        return disallowed.Count == 0
            ? null
            : StatusCode(StatusCodes.Status403Forbidden, ApiResponseFactory.Error(
                ApiErrorCodes.PermissionCeilingExceeded, "One or more permission keys are outside your tenant's permission ceiling.", string.Join(", ", disallowed)));
    }

    private static PermissionGroupSummaryResponse ToSummary(PermissionGroup g, int rolesUsing)
        => new(g.Id, g.Name, g.Description, g.Permissions.Count, rolesUsing, g.IsActive, g.TenantId, g.Tenant?.Name);

    private bool CanDelete(PermissionGroup g) => true; // groups (unlike templates) are always deletable by managers

    private PermissionGroupDetailResponse ToDetail(
        PermissionGroup g,
        IReadOnlyList<(Guid RoleId, string RoleName)> rolesUsing,
        IReadOnlyList<AuditTrailEntry> audit)
        => new(
            g.Id, g.Name, g.Description, g.IsActive, g.TenantId, g.Tenant?.Name,
            g.Permissions.Select(p => p.PermissionKey).OrderBy(k => k).ToList(),
            rolesUsing.Select(r => new RoleUsingGroupResponse(r.RoleId, r.RoleName)).ToList(),
            audit.Select(a => new PermissionGroupAuditEntryResponse(a.Action, a.PerformedBy, a.CreatedDate, a.Details)).ToList(),
            CanDelete(g),
            g.CreatedOnUtc, g.UpdatedOnUtc);
}
