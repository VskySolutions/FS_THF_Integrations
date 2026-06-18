using IntegrationHub.Api.Models.PermissionGroups;
using IntegrationHub.Api.Security;
using IntegrationHub.Application.Abstractions.Auditing;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Security;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Shared.Contracts;
using IntegrationHub.Shared.Security;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Api.Controllers;

/// <summary>
/// Role ↔ Permission Group composition — the bridge in the Permission Keys → Groups → Roles → Users
/// hierarchy. Assigning/removing groups recomputes the role's cached effective permission set, which
/// flows into the JWT <c>permissions[]</c> claim on the next token refresh. Requires roles management.
/// </summary>
[ApiController]
[Route("/api/admin/roles/{roleId:guid}")]
[RequirePermission(Permissions.RolesWrite)]
[Produces("application/json")]
[Tags("Role Composition")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
public sealed class RoleGroupCompositionController : ControllerBase
{
    private readonly IRoleRepository _roles;
    private readonly IPermissionGroupRepository _groups;
    private readonly IPermissionGroupEffectivePermissionService _effective;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;

    public RoleGroupCompositionController(
        IRoleRepository roles,
        IPermissionGroupRepository groups,
        IPermissionGroupEffectivePermissionService effective,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork)
    {
        _roles = roles;
        _groups = groups;
        _effective = effective;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    [HttpGet("groups")]
    [ProducesResponseType<ApiResponse<RoleGroupsResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListGroups(Guid roleId, CancellationToken cancellationToken)
    {
        if (await _roles.GetByIdAsync(roleId, cancellationToken) is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Role not found."));
        }

        var groups = await _groups.GetByRoleAsync(roleId, cancellationToken);
        var preview = await _effective.PreviewForRoleAsync(roleId, cancellationToken);
        var summaries = groups
            .Select(g => new PermissionGroupSummaryResponse(g.Id, g.Name, g.Description, g.Permissions.Count, 0, g.IsActive, g.TenantId, g.Tenant?.Name))
            .ToList();
        return Ok(ApiResponseFactory.Success(new RoleGroupsResponse(roleId, summaries, preview.Permissions), "Role groups retrieved."));
    }

    [HttpGet("permissions/preview")]
    [ProducesResponseType<ApiResponse<RolePermissionPreviewResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Preview(Guid roleId, CancellationToken cancellationToken)
    {
        if (await _roles.GetByIdAsync(roleId, cancellationToken) is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Role not found."));
        }

        var preview = await _effective.PreviewForRoleAsync(roleId, cancellationToken);
        var sources = preview.Sources
            .Select(s => new EffectivePermissionSourceResponse(s.GroupId, s.GroupName, s.IsActive, s.Keys))
            .ToList();
        return Ok(ApiResponseFactory.Success(new RolePermissionPreviewResponse(preview.Permissions, sources), "Preview computed."));
    }

    [HttpPost("groups")]
    [ProducesResponseType<ApiResponse<RoleGroupsResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignGroups(Guid roleId, [FromBody] AssignGroupsRequest body, CancellationToken cancellationToken)
    {
        var role = await _roles.GetByIdAsync(roleId, cancellationToken);
        if (role is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Role not found."));
        }

        // Load and validate every requested group: must exist, belong to one tenant, and be in the
        // caller's tenant unless they are a Super Admin.
        var loaded = new List<PermissionGroup>();
        foreach (var groupId in body.GroupIds.Distinct())
        {
            var group = await _groups.GetByIdUnscopedAsync(groupId, cancellationToken);
            if (group is null)
            {
                return NotFound(ApiResponseFactory.NotFound($"Permission group {groupId} not found."));
            }
            loaded.Add(group);
        }
        if (loaded.Select(g => g.TenantId).Distinct().Count() > 1)
        {
            return BadRequest(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed, "All groups must belong to the same tenant.", string.Empty));
        }
        if (!User.IsSuperAdmin() && loaded.Any(g => g.TenantId != User.GetActiveTenantId()))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponseFactory.Forbidden("Groups must belong to your tenant."));
        }

        foreach (var group in loaded)
        {
            if (await _groups.GetRoleLinkAsync(roleId, group.Id, cancellationToken) is null)
            {
                await _groups.AddRoleLinkAsync(new RolePermissionGroup { Id = Guid.NewGuid(), RoleId = roleId, PermissionGroupId = group.Id }, cancellationToken);
                await _audit.AddAsync(nameof(Role), roleId.ToString(), "PermissionGroupAssigned", details: group.Name, cancellationToken: cancellationToken);
                await _audit.AddAsync(nameof(PermissionGroup), group.Id.ToString(), "AssignedToRole", details: role.Name, cancellationToken: cancellationToken);
            }
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _effective.RecomputeForRoleAsync(roleId, cancellationToken);

        var preview = await _effective.PreviewForRoleAsync(roleId, cancellationToken);
        var groups = await _groups.GetByRoleAsync(roleId, cancellationToken);
        var summaries = groups
            .Select(g => new PermissionGroupSummaryResponse(g.Id, g.Name, g.Description, g.Permissions.Count, 0, g.IsActive, g.TenantId, g.Tenant?.Name))
            .ToList();
        return Ok(ApiResponseFactory.Success(new RoleGroupsResponse(roleId, summaries, preview.Permissions), "Groups assigned to role."));
    }

    [HttpDelete("groups/{groupId:guid}")]
    [ProducesResponseType<ApiResponse<RemoveGroupFromRoleResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveGroup(Guid roleId, Guid groupId, CancellationToken cancellationToken)
    {
        var role = await _roles.GetByIdAsync(roleId, cancellationToken);
        if (role is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Role not found."));
        }
        var link = await _groups.GetRoleLinkAsync(roleId, groupId, cancellationToken);
        if (link is null)
        {
            return NotFound(ApiResponseFactory.NotFound("The group is not assigned to this role."));
        }

        var removedGroup = await _groups.GetByIdUnscopedAsync(groupId, cancellationToken);
        var removedKeys = removedGroup is { IsActive: true }
            ? removedGroup.Permissions.Select(p => p.PermissionKey).Distinct(StringComparer.Ordinal).ToList()
            : new List<string>();

        _groups.RemoveRoleLink(link);
        await _audit.AddAsync(nameof(Role), roleId.ToString(), "PermissionGroupRemoved", details: removedGroup?.Name, cancellationToken: cancellationToken);
        await _audit.AddAsync(nameof(PermissionGroup), groupId.ToString(), "RemovedFromRole", details: role.Name, cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _effective.RecomputeForRoleAsync(roleId, cancellationToken);

        var preview = await _effective.PreviewForRoleAsync(roleId, cancellationToken);
        // Keys that were the removed group's and are no longer provided by any remaining group.
        var lost = removedKeys.Where(k => !preview.Permissions.Contains(k)).ToList();
        return Ok(ApiResponseFactory.Success(new RemoveGroupFromRoleResponse(preview.Permissions, lost), "Group removed from role."));
    }
}
