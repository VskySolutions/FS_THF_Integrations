using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;

namespace EmsPortal.Application.Abstractions.Persistence;

/// <summary>Data access for <see cref="Note"/>s and their <see cref="NoteMention"/>s.</summary>
public interface INoteRepository
{
    Task AddAsync(Note note, CancellationToken cancellationToken = default);

    /// <summary>Loads a note (including its mentions) by id, or null when missing/soft-deleted.</summary>
    Task<Note?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Update(Note note);

    void Remove(Note note);

    /// <summary>Paginated, newest-first notes for a record; optional body search and author filter.</summary>
    Task<(IReadOnlyList<Note> Items, int Total)> ListAsync(
        EntityType entityType, Guid entityId, string? search, Guid? authorId, int page, int limit, CancellationToken cancellationToken = default);

    Task AddMentionAsync(NoteMention mention, CancellationToken cancellationToken = default);

    void RemoveMention(NoteMention mention);

    /// <summary>Paginated @mentions of a user (joined to their note), newest-first; optional entity-type and read filters.</summary>
    Task<(IReadOnlyList<(NoteMention Mention, Note Note)> Items, int Total)> ListMentionsForUserAsync(
        Guid userId, EntityType? entityType, bool? isRead, int page, int limit, CancellationToken cancellationToken = default);

    /// <summary>A single mention belonging to the user, or null.</summary>
    Task<NoteMention?> GetMentionForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}
