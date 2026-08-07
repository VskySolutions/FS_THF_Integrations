using EmsPortal.Domain.Enums;

namespace EmsPortal.Domain.Entities;

/// <summary>
/// Junction assigning a <see cref="User"/> a single RBAC <see cref="Entities.Role"/> within a tenant.
/// One row per (user, tenant, role): a user may hold multiple roles in the same tenant, each its own
/// row (WO-122). The role assignment set is reconciled on assign — rows are added/soft-deleted so the
/// active set matches the request (AC-ADM-006.2).
/// </summary>
public class UserTenantRole : AuditableEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>
    /// Legacy fixed-tier role. Retained during the RBAC transition; operational roles map to
    /// <see cref="UserRole.Custom"/>. The role NAME (<see cref="RoleEntity"/>.Name) now drives the JWT
    /// role claim and authorization, so this is a display/back-compat shadow of <see cref="RoleId"/>.
    /// </summary>
    public UserRole Role { get; set; }

    /// <summary>
    /// The assigned RBAC <see cref="Entities.Role"/>. Required — every row references exactly one
    /// concrete role (system or custom).
    /// </summary>
    public Guid RoleId { get; set; }

    public User? User { get; set; }

    public Role? RoleEntity { get; set; }
}
