using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Api.Models.UniversalFeatures;

/// <summary>A user @mention entry for the Mention Inbox.</summary>
public sealed record MentionResponse(
    Guid Id,
    Guid NoteId,
    EntityType EntityType,
    Guid EntityId,
    Guid? AuthorId,
    string? AuthorName,
    string Preview,
    bool IsRead,
    DateTime CreatedOnUtc);

/// <summary>A candidate user for the note @mention autocomplete.</summary>
public sealed record MentionCandidateResponse(Guid UserId, string Name, string? Email);
