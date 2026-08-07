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

public sealed record RoleResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    IReadOnlyList<string> Permissions,
    DateTime CreatedOnUtc,
    DateTime UpdatedOnUtc);

/// <summary>
/// A role as the list shows it. The trailing four are the audit trail every list offers as
/// hidden-by-default columns; the *By names are resolved by the controller.
/// </summary>
public sealed record RoleSummary(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    int PermissionCount,
    string? CreatedBy,
    DateTime CreatedOnUtc,
    string? UpdatedBy,
    DateTime UpdatedOnUtc);
