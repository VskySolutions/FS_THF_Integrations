using EmsPortal.Domain.Enums;

namespace EmsPortal.Domain.Entities;

/// <summary>
/// A tenant-managed label that can be applied to any entity record. Name is unique per tenant.
/// </summary>
public class Tag : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped). Names are unique within a tenant.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Tag display name, unique per tenant.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Hex/named colour for the chip.</summary>
    public string? Colour { get; set; }

    /// <summary>Optional grouping category for the tag picker.</summary>
    public string? Category { get; set; }

    /// <summary>Records this tag is currently applied to.</summary>
    public ICollection<EntityTag> EntityTags { get; set; } = new List<EntityTag>();
}

/// <summary>The application of a <see cref="Tag"/> to a specific entity record.</summary>
public class EntityTag : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The kind of entity the tag is applied to.</summary>
    public EntityType EntityType { get; set; }

    /// <summary>The id of the entity the tag is applied to.</summary>
    public Guid EntityId { get; set; }

    /// <summary>The applied tag.</summary>
    public Guid TagId { get; set; }

    public Tag? Tag { get; set; }
}
