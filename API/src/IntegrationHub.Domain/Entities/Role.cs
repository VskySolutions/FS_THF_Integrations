namespace IntegrationHub.Domain.Entities;

/// <summary>
/// A named set of permissions (RBAC). Created and managed by Super Admins, made
/// available to tenants via <see cref="TenantRole"/>, and assigned to users.
/// System roles (SuperAdmin/TenantAdmin/Operator) are seeded and cannot be deleted.
/// </summary>
public class Role : AuditableEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Seeded, non-deletable role mirroring a base authorization level.</summary>
    public bool IsSystem { get; set; }

    /// <summary>Permission keys (see Shared.Security.Permissions). Mapped as a JSON column.</summary>
    public List<string> Permissions { get; set; } = new();
}
