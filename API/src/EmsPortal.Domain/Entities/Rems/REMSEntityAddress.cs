using EmsPortal.Domain.Enums;

namespace EmsPortal.Domain.Entities;

/// <summary>
/// An address of a <see cref="REMSEntity"/> (WO-110). At most one address per
/// (entity, <see cref="AddressType"/>). Links to a shared <see cref="Address"/> record.
/// </summary>
public class REMSEntityAddress : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Owning entity.</summary>
    public Guid REMSEntityId { get; set; }

    /// <summary>The shared address record.</summary>
    public Guid AddressId { get; set; }

    /// <summary>Whether this is the physical or mailing address.</summary>
    public RemsAddressType AddressType { get; set; }

    // ---- Navigations ----
    public REMSEntity? Entity { get; set; }
    public Address? Address { get; set; }
}
