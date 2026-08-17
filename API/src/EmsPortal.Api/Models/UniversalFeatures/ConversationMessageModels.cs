using EmsPortal.Domain.Enums;

namespace EmsPortal.Api.Models.UniversalFeatures;

/// <summary>Request to post a message to a record's conversation.</summary>
public sealed class CreateConversationMessageRequest
{
    public EntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public string Body { get; set; } = string.Empty;

    /// <summary>Explicitly @mentioned user ids (resolved from the editor autocomplete).</summary>
    public List<Guid>? MentionedUserIds { get; set; }
}

/// <summary>Request to edit an existing conversation message.</summary>
public sealed class UpdateConversationMessageRequest
{
    public string Body { get; set; } = string.Empty;
    public List<Guid>? MentionedUserIds { get; set; }
}

/// <summary>A conversation message as returned to the client.</summary>
public sealed record ConversationMessageResponse(
    Guid Id,
    EntityType EntityType,
    Guid EntityId,
    string Body,
    Guid? AuthorId,
    string? AuthorName,
    bool IsEdited,
    DateTime? EditedOnUtc,
    DateTime CreatedOnUtc,
    IReadOnlyList<Guid> MentionedUserIds);
