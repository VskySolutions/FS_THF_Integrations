using EmsPortal.Domain.Entities;

namespace EmsPortal.Application.Abstractions.Persistence;

/// <summary>Data access for personal <see cref="Reminder"/>s.</summary>
public interface IReminderRepository
{
    Task AddAsync(Reminder reminder, CancellationToken cancellationToken = default);

    Task<Reminder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Update(Reminder reminder);

    void Remove(Reminder reminder);

    /// <summary>Paginated reminders owned by a user, soonest-due first.</summary>
    Task<(IReadOnlyList<Reminder> Items, int Total)> ListByUserAsync(Guid userId, int page, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// All undispatched reminders due at or before <paramref name="nowUtc"/>, across every tenant — used
    /// by the dispatch job which runs without a resolved tenant (so the ambient filter is a no-op).
    /// </summary>
    Task<IReadOnlyList<Reminder>> GetDueUndispatchedAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
}
