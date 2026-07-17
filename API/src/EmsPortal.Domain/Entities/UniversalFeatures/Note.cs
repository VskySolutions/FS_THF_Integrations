using EmsPortal.Domain.Enums;

namespace EmsPortal.Domain.Entities;

/// <summary>
/// A freeform, @mention-aware note attached to any entity via the shared <c>(EntityType, EntityId)</c>
/// key. Soft-deletable (the inherited <see cref="AuditableEntity.Deleted"/> flag is the delete state).
/// </summary>
public class Note : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The kind of entity this note is attached to.</summary>
    public EntityType EntityType { get; set; }

    /// <summary>The id of the entity this note is attached to.</summary>
    public Guid EntityId { get; set; }

    /// <summary>The note text, including raw <c>@mention</c> tokens.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>True once the author has edited the note after creation.</summary>
    public bool IsEdited { get; set; }

    /// <summary>UTC timestamp of the last edit; null while never edited.</summary>
    public DateTime? EditedOnUtc { get; set; }

    /// <summary>The users @mentioned in this note.</summary>
    public ICollection<NoteMention> Mentions { get; set; } = new List<NoteMention>();
}

/// <summary>A resolved @mention of a user within a <see cref="Note"/>.</summary>
public class NoteMention : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The note that contains the mention.</summary>
    public Guid NoteId { get; set; }

    /// <summary>The mentioned user.</summary>
    public Guid MentionedUserId { get; set; }

    /// <summary>True once the mentioned user has read the mention (Mention Inbox).</summary>
    public bool IsRead { get; set; }

    public Note? Note { get; set; }
}
