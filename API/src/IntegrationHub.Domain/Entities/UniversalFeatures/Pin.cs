using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Domain.Entities;

/// <summary>
/// A user's pin (bookmark) of an entity record, surfaced on the "My Pinned" list. Unique per
/// <c>(UserId, EntityType, EntityId)</c>; a per-user maximum (50) is enforced in the service layer.
/// </summary>
public class Pin : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The user who pinned the record.</summary>
    public Guid UserId { get; set; }

    /// <summary>The kind of entity pinned.</summary>
    public EntityType EntityType { get; set; }

    /// <summary>The id of the entity pinned.</summary>
    public Guid EntityId { get; set; }

    /// <summary>When the record was pinned (UTC).</summary>
    public DateTime PinnedOnUtc { get; set; }
}
