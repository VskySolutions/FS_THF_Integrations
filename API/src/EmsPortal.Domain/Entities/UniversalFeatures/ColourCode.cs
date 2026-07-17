using EmsPortal.Domain.Enums;

namespace EmsPortal.Domain.Entities;

/// <summary>
/// A user's personal colour assignment for an entity record, used to colour-tag rows on list
/// pages. Upserted per <c>(UserId, EntityType, EntityId)</c>; clearing the colour removes the row.
/// </summary>
public class ColourCode : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The user the colour assignment belongs to.</summary>
    public Guid UserId { get; set; }

    /// <summary>The kind of entity coloured.</summary>
    public EntityType EntityType { get; set; }

    /// <summary>The id of the entity coloured.</summary>
    public Guid EntityId { get; set; }

    /// <summary>Hex/named colour value.</summary>
    public string Colour { get; set; } = string.Empty;
}
