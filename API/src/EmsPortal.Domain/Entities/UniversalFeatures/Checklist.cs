using EmsPortal.Domain.Enums;

namespace EmsPortal.Domain.Entities;

/// <summary>
/// A checklist attached to any entity record via the shared <c>(EntityType, EntityId)</c> key.
/// </summary>
public class Checklist : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The kind of entity the checklist is attached to.</summary>
    public EntityType EntityType { get; set; }

    /// <summary>The id of the entity the checklist is attached to.</summary>
    public Guid EntityId { get; set; }

    /// <summary>Checklist title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The checklist's items.</summary>
    public ICollection<ChecklistItem> Items { get; set; } = new List<ChecklistItem>();
}

/// <summary>A single, completable item within a <see cref="Checklist"/>.</summary>
public class ChecklistItem : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The parent checklist.</summary>
    public Guid ChecklistId { get; set; }

    /// <summary>Item text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>True when the item is checked off.</summary>
    public bool IsCompleted { get; set; }

    /// <summary>The user who completed the item; null while incomplete.</summary>
    public Guid? CompletedById { get; set; }

    /// <summary>When the item was completed (UTC); null while incomplete.</summary>
    public DateTime? CompletedOnUtc { get; set; }

    /// <summary>Manual ordering position within the checklist (drag-and-drop reorder).</summary>
    public int SortOrder { get; set; }

    public Checklist? Checklist { get; set; }
}
