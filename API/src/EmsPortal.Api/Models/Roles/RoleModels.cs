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
/// A single role. <paramref name="TenantId"/> is null for a platform role and set for one a tenant
/// created for itself; <paramref name="CanManage"/> says whether THIS caller may edit or delete it, so
/// the client gates its buttons on the same rule the server enforces rather than a guess at it.
/// </summary>
public sealed record RoleResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    Guid? TenantId,
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
