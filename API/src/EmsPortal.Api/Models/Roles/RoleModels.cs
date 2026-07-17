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

public sealed record RoleSummary(Guid Id, string Name, string? Description, bool IsSystem, int PermissionCount);
