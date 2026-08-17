using EmsPortal.Domain.Enums;

namespace EmsPortal.Domain.Entities;

/// <summary>
/// One message in a record's conversation — freeform, @mention-aware, attached via the shared
/// <c>(EntityType, EntityId)</c> key. There is no Conversation row: the conversation IS the thread of
/// messages sharing that key, which is why a record has one conversation and many messages.
/// Soft-deletable (the inherited <see cref="AuditableEntity.Deleted"/> flag is the delete state).
/// </summary>
public class ConversationMessage : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The kind of entity this message is attached to.</summary>
    public EntityType EntityType { get; set; }

    /// <summary>The id of the entity this message is attached to.</summary>
    public Guid EntityId { get; set; }

    /// <summary>The message text, including raw <c>@mention</c> tokens.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>True once the author has edited the message after posting it.</summary>
    public bool IsEdited { get; set; }

    /// <summary>UTC timestamp of the last edit; null while never edited.</summary>
    public DateTime? EditedOnUtc { get; set; }

    /// <summary>The users @mentioned in this message.</summary>
    public ICollection<ConversationMessageMention> Mentions { get; set; } = new List<ConversationMessageMention>();
}

/// <summary>A resolved @mention of a user within a <see cref="ConversationMessage"/>.</summary>
public class ConversationMessageMention : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The message that contains the mention.</summary>
    public Guid ConversationMessageId { get; set; }

    /// <summary>The mentioned user.</summary>
    public Guid MentionedUserId { get; set; }

    /// <summary>True once the mentioned user has read the mention (Mention Inbox).</summary>
    public bool IsRead { get; set; }

    public ConversationMessage? ConversationMessage { get; set; }
}
