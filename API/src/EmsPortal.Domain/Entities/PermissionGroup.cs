namespace EmsPortal.Domain.Entities;

/// <summary>
/// A named, tenant-scoped bundle of permission keys (RBAC three-tier hierarchy:
/// Permission Keys → Permission Groups → Roles → Users). Groups are composed into
/// <see cref="Role"/>s via <see cref="RolePermissionGroup"/>; a role's effective
/// permissions are the union of all its active groups' keys. Soft-deletable.
/// </summary>
public class PermissionGroup : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped). Names are unique within a tenant.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Group name, unique per tenant.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional human-readable description.</summary>
    public string? Description { get; set; }

    /// <summary>Whether the group is active. An inactive group contributes zero permissions to any role.</summary>
    public bool IsActive { get; set; } = true;

    // ---- Navigations ----
    public Tenant? Tenant { get; set; }
    public ICollection<PermissionGroupPermission> Permissions { get; set; } = new List<PermissionGroupPermission>();
    public ICollection<RolePermissionGroup> RoleLinks { get; set; } = new List<RolePermissionGroup>();
}
