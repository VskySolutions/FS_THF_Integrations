namespace EmsPortal.Domain.Entities;

/// <summary>
/// Join entity linking a <see cref="User"/> to a <see cref="UserGroup"/> (many-to-many).
/// Tenant-scoped (mirrors the group's tenant). Soft-deletable so memberships can be revoked.
/// </summary>
public class UserGroupMember : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant (matches <see cref="UserGroup.TenantId"/>).</summary>
    public Guid TenantId { get; set; }

    public Guid UserGroupId { get; set; }

    public Guid UserId { get; set; }

    // ---- Navigations ----
    public UserGroup? UserGroup { get; set; }
    public User? User { get; set; }
}
