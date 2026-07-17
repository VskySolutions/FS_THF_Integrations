using EmsPortal.Domain.Enums;

namespace EmsPortal.Domain.Entities;

/// <summary>
/// Junction assigning a <see cref="User"/> a <see cref="UserRole"/> within a tenant.
/// Unique per (user, tenant); reassignment updates the role rather than duplicating
/// (AC-ADM-006.2).
/// </summary>
public class UserTenantRole : AuditableEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>
    /// Legacy fixed-tier role. Retained during the RBAC transition as the source for the
    /// JWT role claim and the role-based authorization policies. Kept in sync with
    /// <see cref="RoleId"/>.
    /// </summary>
    public UserRole Role { get; set; }

    /// <summary>
    /// The assigned RBAC <see cref="Entities.Role"/>. Nullable during the transition; for
    /// legacy/enum-driven assignments it points at the matching system role.
    /// </summary>
    public Guid? RoleId { get; set; }

    public User? User { get; set; }

    public Role? RoleEntity { get; set; }
}
