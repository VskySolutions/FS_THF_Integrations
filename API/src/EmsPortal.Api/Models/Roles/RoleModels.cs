namespace EmsPortal.Api.Models.Roles;

public sealed class CreateRoleRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Permissions { get; set; } = new();
}

public sealed class UpdateRoleRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public List<string>? Permissions { get; set; }
}

public sealed class AssignRoleToTenantRequest
{
    public Guid RoleId { get; set; }
}

/// <summary>
/// A single role. <paramref name="TenantId"/> (with <paramref name="TenantName"/>) is null for a platform
/// role and set for one a tenant created for itself; <paramref name="CanManage"/> says whether THIS caller
/// may edit or delete it, so the client gates its buttons on the same rule the server enforces rather than
/// on a guess at it.
/// </summary>
public sealed record RoleResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    Guid? TenantId,
    string? TenantName,
    bool CanManage,
    IReadOnlyList<string> Permissions,
    DateTime CreatedOnUtc,
    DateTime UpdatedOnUtc);

/// <summary>
/// A role as the list shows it. <paramref name="TenantName"/> names the owning tenant (null for a
/// platform role) so the list can say where each row comes from. The trailing four are the audit trail
/// every list offers as hidden-by-default columns; the *By names are resolved by the controller.
/// </summary>
public sealed record RoleSummary(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    Guid? TenantId,
    string? TenantName,
    bool CanManage,
    int PermissionCount,
    string? CreatedBy,
    DateTime CreatedOnUtc,
    string? UpdatedBy,
    DateTime UpdatedOnUtc);

// ---- Role membership (who holds a role in a tenant) ----

/// <summary>
/// Somebody holding the role in this tenant. <paramref name="OtherRoles"/> is what else they hold here,
/// which is the context for taking this one away; <paramref name="IsOnlyRole"/> says the role IS their
/// access to the tenant, so removing it here is refused.
/// </summary>
public sealed record RoleMemberResponse(
    Guid UserId,
    string DisplayName,
    string? Email,
    bool IsActive,
    IReadOnlyList<string> OtherRoles,
    bool IsOnlyRole);

/// <summary>A user the role could be given to: someone already in the tenant who does not hold it yet.</summary>
public sealed record RoleMemberCandidateResponse(Guid UserId, string DisplayName, string? Email);

public sealed class AddRoleMembersRequest
{
    public List<Guid> UserIds { get; set; } = new();
}
