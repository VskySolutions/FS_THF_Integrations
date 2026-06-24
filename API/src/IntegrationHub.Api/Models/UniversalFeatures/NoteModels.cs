using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Api.Models.UniversalFeatures;

/// <summary>Request to create a note on an entity record.</summary>
public sealed class CreateNoteRequest
{
    public EntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public string Body { get; set; } = string.Empty;

    /// <summary>Explicitly @mentioned user ids (resolved from the editor autocomplete).</summary>
    public List<Guid>? MentionedUserIds { get; set; }
}

/// <summary>Request to edit an existing note.</summary>
public sealed class UpdateNoteRequest
{
    public string Body { get; set; } = string.Empty;
    public List<Guid>? MentionedUserIds { get; set; }
}

/// <summary>A note as returned to the client.</summary>
public sealed record NoteResponse(
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
