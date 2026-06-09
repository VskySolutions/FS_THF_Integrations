using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Domain.Entities;

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

    public UserRole Role { get; set; }

    public User? User { get; set; }
}
