using EmsPortal.Domain.Entities;

namespace EmsPortal.Application.Abstractions.Persistence;

/// <summary>Data access for <see cref="StickyNote"/>s, their dismissals, and per-user layout state.</summary>
public interface IStickyNoteRepository
{
    Task AddAsync(StickyNote note, CancellationToken cancellationToken = default);

    Task<StickyNote?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Update(StickyNote note);

    void Remove(StickyNote note);

    /// <summary>
    /// Active notes visible to a user: their own personal notes plus undismissed tenant notes, filtered
    /// to those whose scope is <c>global</c> or matches <paramref name="scope"/> (when supplied).
    /// </summary>
    Task<IReadOnlyList<StickyNote>> ListActiveForUserAsync(Guid userId, string? scope, CancellationToken cancellationToken = default);

    /// <summary>All tenant (non-personal) notes with their dismissal counts, for the admin management list.</summary>
    Task<IReadOnlyList<(StickyNote Note, int DismissalCount)>> ListTenantNotesWithCountsAsync(CancellationToken cancellationToken = default);

    /// <summary>Expired tenant notes across all tenants — used by the hourly expiry sweep.</summary>
    Task<IReadOnlyList<StickyNote>> GetExpiredTenantNotesAsync(DateTime nowUtc, CancellationToken cancellationToken = default);

    // ---- Dismissals ----
    Task<StickyNoteDismissal?> GetDismissalAsync(Guid noteId, Guid userId, CancellationToken cancellationToken = default);

    Task AddDismissalAsync(StickyNoteDismissal dismissal, CancellationToken cancellationToken = default);

    /// <summary>Clears all dismissals for a note (re-surfaces a tenant note after an edit).</summary>
    Task<IReadOnlyList<StickyNoteDismissal>> GetDismissalsByNoteAsync(Guid noteId, CancellationToken cancellationToken = default);

    void RemoveDismissal(StickyNoteDismissal dismissal);

    // ---- Per-user layout state ----
    Task<UserStickyNoteState?> GetStateAsync(Guid noteId, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, UserStickyNoteState>> GetStatesAsync(Guid userId, IReadOnlyCollection<Guid> noteIds, CancellationToken cancellationToken = default);

    Task AddStateAsync(UserStickyNoteState state, CancellationToken cancellationToken = default);

    void UpdateState(UserStickyNoteState state);
}
