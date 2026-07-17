using EmsPortal.Domain.Enums;

namespace EmsPortal.Domain.Entities;

/// <summary>
/// An append-only, user-facing event in a record's activity timeline. Written in-process by
/// <c>ActivityEventWriter</c> whenever a module performs a tracked action (Universal Features ADR-002).
/// Write-once: never updated or deleted via any API. <see cref="AuditableEntity.CreatedOnUtc"/> is the
/// event timestamp and <see cref="AuditableEntity.CreatedById"/> mirrors <see cref="ActorId"/>.
/// </summary>
public class ActivityEvent : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The kind of entity this event is attached to.</summary>
    public EntityType EntityType { get; set; }

    /// <summary>The id of the entity this event is attached to.</summary>
    public Guid EntityId { get; set; }

    /// <summary>The event kind — see <see cref="ActivityEventTypes"/> (open set, stored as text).</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>The user who caused the event; null for system-generated events.</summary>
    public Guid? ActorId { get; set; }

    /// <summary>Previous value (for change-style events); null when not applicable.</summary>
    public string? OldValue { get; set; }

    /// <summary>New value (for change-style events); null when not applicable.</summary>
    public string? NewValue { get; set; }
}

/// <summary>The well-known activity event type keys (open set — modules may add new values).</summary>
public static class ActivityEventTypes
{
    public const string StatusChanged = "StatusChanged";
    public const string FieldEdited = "FieldEdited";
    public const string NoteAdded = "NoteAdded";
    public const string TagApplied = "TagApplied";
    public const string TagRemoved = "TagRemoved";
    public const string AttachmentUploaded = "AttachmentUploaded";
    public const string AttachmentDeleted = "AttachmentDeleted";
    public const string ChecklistItemCompleted = "ChecklistItemCompleted";
    public const string Restored = "Restored";
}
