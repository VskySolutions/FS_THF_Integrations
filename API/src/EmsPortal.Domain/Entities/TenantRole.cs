namespace EmsPortal.Domain.Entities;

/// <summary>
/// Makes a <see cref="Role"/> available to a <see cref="Tenant"/>. Super Admins
/// assign roles to tenants; Tenant Admins may then assign those roles to their users.
/// </summary>
public class TenantRole : AuditableEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid RoleId { get; set; }
}
