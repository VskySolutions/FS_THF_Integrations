namespace EmsPortal.Domain.Entities;

/// <summary>
/// A named, tenant-scoped grouping of users (e.g. a team, department or distribution list).
/// Independent of RBAC roles/permission groups — used to segment and query users by group name.
/// Soft-deletable.
/// </summary>
public class UserGroup : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped). Names are unique within a tenant.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Group name, unique per tenant.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional human-readable description.</summary>
    public string? Description { get; set; }

    // ---- Navigations ----
    public Tenant? Tenant { get; set; }
    public ICollection<UserGroupMember> Members { get; set; } = new List<UserGroupMember>();
}
