using IntegrationHub.Api.Models.Roles;
using IntegrationHub.Api.Security;
using IntegrationHub.Application.Abstractions.Auditing;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Shared.Contracts;
using IntegrationHub.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Api.Controllers;

/// <summary>
/// RBAC role management (Phase 1 foundation). Super Admins CRUD roles and assign
/// them to tenants; Tenant Admins read the roles available to their tenant so they
/// can assign them to users. Authorization still uses the base-level policies during
/// the RBAC rollout.
/// </summary>
[ApiController]
[Produces("application/json")]
[Tags("Roles")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status500InternalServerError)]
public sealed class RolesController : ControllerBase
{
    private readonly IRoleRepository _roles;
    private readonly ITenantRepository _tenants;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;

    public RolesController(IRoleRepository roles, ITenantRepository tenants, IAuditTrailService audit, IUnitOfWork unitOfWork)
    {
        _roles = roles;
        _tenants = tenants;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    [HttpGet("/api/admin/permissions")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdminOrAbove)]
    public IActionResult Catalog()
        => Ok(ApiResponseFactory.Success(Permissions.All, "Permissions retrieved."));

    [HttpGet("/api/admin/roles")]
    [Authorize(Policy = AuthorizationPolicies.SuperAdminOnly)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var roles = await _roles.ListAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(roles.Select(ToSummary), "Roles retrieved."));
    }

    [HttpGet("/api/admin/roles/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.SuperAdminOnly)]
    [ProducesResponseType<ApiResponse<RoleResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var role = await _roles.GetByIdAsync(id, cancellationToken);
        return role is null
            ? NotFound(ApiResponseFactory.NotFound("Role not found."))
            : Ok(ApiResponseFactory.Success(ToResponse(role), "Role retrieved."));
    }

    [HttpPost("/api/admin/roles")]
    [Authorize(Policy = AuthorizationPolicies.SuperAdminOnly)]
    [ProducesResponseType<ApiResponse<RoleResponse>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var invalid = InvalidPermissions(request.Permissions);
        if (invalid is not null)
        {
            return invalid;
        }

        if (await _roles.NameExistsAsync(request.Name, cancellationToken))
        {
            return Conflict(ApiResponseFactory.Error(ApiErrorCodes.DuplicateIdentifier, "Role name already in use.", request.Name));
        }

        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            IsSystem = false,
            Permissions = Normalize(request.Permissions),
        };
        await _roles.AddAsync(role, cancellationToken);
        await _audit.AddAsync(nameof(Role), role.Id.ToString(), "Created", details: role.Name, cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponseFactory.Success(ToResponse(role), "Role created."));
    }

    [HttpPut("/api/admin/roles/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.SuperAdminOnly)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await _roles.GetByIdAsync(id, cancellationToken);
        if (role is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Role not found."));
        }

        if (request.Permissions is not null)
        {
            var invalid = InvalidPermissions(request.Permissions);
            if (invalid is not null)
            {
                return invalid;
            }
            role.Permissions = Normalize(request.Permissions);
        }

        // System role names are fixed; their permission sets may still be tuned.
        if (!role.IsSystem && !string.IsNullOrWhiteSpace(request.Name))
        {
            role.Name = request.Name;
        }
        if (request.Description is not null)
        {
            role.Description = request.Description;
        }

        _roles.Update(role);
        await _audit.AddAsync(nameof(Role), role.Id.ToString(), "Updated", cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(ToResponse(role), "Role updated."));
    }

    [HttpDelete("/api/admin/roles/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.SuperAdminOnly)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var role = await _roles.GetByIdAsync(id, cancellationToken);
        if (role is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Role not found."));
        }
        if (role.IsSystem)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponseFactory.Forbidden("System roles cannot be deleted."));
        }

        _roles.Remove(role); // soft delete via interceptor
        await _audit.AddAsync(nameof(Role), role.Id.ToString(), "Deleted", cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(new { message = "Role deleted." }, "Role deleted."));
    }

    // ---- Tenant ↔ role availability ----

    [HttpGet("/api/admin/tenants/{tenantId:guid}/roles")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdminOrAbove)]
    public async Task<IActionResult> ListForTenant(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!User.IsSuperAdmin() && User.GetActiveTenantId() != tenantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponseFactory.Forbidden("Not permitted for this tenant."));
        }

        var roles = await _roles.ListByTenantAsync(tenantId, cancellationToken);
        return Ok(ApiResponseFactory.Success(roles.Select(ToSummary), "Tenant roles retrieved."));
    }

    [HttpPost("/api/admin/tenants/{tenantId:guid}/roles")]
    [Authorize(Policy = AuthorizationPolicies.SuperAdminOnly)]
    public async Task<IActionResult> AssignToTenant(Guid tenantId, [FromBody] AssignRoleToTenantRequest request, CancellationToken cancellationToken)
    {
        if (await _tenants.GetByIdAsync(tenantId, cancellationToken) is null)
        {
            return NotFound(ApiResponseFactory.Error(ApiErrorCodes.TenantNotFound, "Tenant not found.", tenantId.ToString()));
        }
        if (await _roles.GetByIdAsync(request.RoleId, cancellationToken) is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Role not found."));
        }

        if (await _roles.GetTenantRoleAsync(tenantId, request.RoleId, cancellationToken) is null)
        {
            await _roles.AddTenantRoleAsync(new TenantRole { Id = Guid.NewGuid(), TenantId = tenantId, RoleId = request.RoleId }, cancellationToken);
            await _audit.AddAsync(nameof(TenantRole), tenantId.ToString(), "RoleAssigned", details: request.RoleId.ToString(), cancellationToken: cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Ok(ApiResponseFactory.Success(new { tenantId, roleId = request.RoleId }, "Role assigned to tenant."));
    }

    [HttpDelete("/api/admin/tenants/{tenantId:guid}/roles/{roleId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.SuperAdminOnly)]
    public async Task<IActionResult> UnassignFromTenant(Guid tenantId, Guid roleId, CancellationToken cancellationToken)
    {
        var existing = await _roles.GetTenantRoleAsync(tenantId, roleId, cancellationToken);
        if (existing is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Tenant role assignment not found."));
        }

        _roles.RemoveTenantRole(existing);
        await _audit.AddAsync(nameof(TenantRole), tenantId.ToString(), "RoleUnassigned", details: roleId.ToString(), cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(new { message = "Role unassigned." }, "Role unassigned."));
    }

    // ---- helpers ----

    private IActionResult? InvalidPermissions(IEnumerable<string> permissions)
    {
        var unknown = permissions.Where(p => !Permissions.All.Contains(p)).ToList();
        return unknown.Count == 0
            ? null
            : BadRequest(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed, "Unknown permission(s).", string.Join(", ", unknown)));
    }

    private static List<string> Normalize(IEnumerable<string> permissions)
        => permissions.Where(Permissions.All.Contains).Distinct().ToList();

    private static RoleResponse ToResponse(Role r) => new(r.Id, r.Name, r.Description, r.IsSystem, r.Permissions, r.CreatedOnUtc, r.UpdatedOnUtc);

    private static RoleSummary ToSummary(Role r) => new(r.Id, r.Name, r.Description, r.IsSystem, r.Permissions.Count);
}
