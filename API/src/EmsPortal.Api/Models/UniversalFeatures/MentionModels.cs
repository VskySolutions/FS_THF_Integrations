using EmsPortal.Domain.Enums;

namespace EmsPortal.Api.Models.UniversalFeatures;

/// <summary>A user @mention entry for the Mention Inbox.</summary>
public sealed record MentionResponse(
    Guid Id,
    Guid ConversationMessageId,
    EntityType EntityType,
    Guid EntityId,
    Guid? AuthorId,
    string? AuthorName,
    string Preview,
    bool IsRead,
    DateTime CreatedOnUtc);

/// <summary>A candidate user for the conversation @mention autocomplete.</summary>
public sealed record MentionCandidateResponse(Guid UserId, string Name, string? Email);
