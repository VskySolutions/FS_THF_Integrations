using EmsPortal.Domain.Entities;

namespace EmsPortal.Application.Abstractions.Persistence;

/// <summary>Data access for <see cref="SavedView"/>s (private and tenant-shared list configurations).</summary>
public interface ISavedViewRepository
{
    Task AddAsync(SavedView view, CancellationToken cancellationToken = default);

    Task<SavedView?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Update(SavedView view);

    void Remove(SavedView view);

    /// <summary>The user's private views plus the tenant's shared views for a list page (private first).</summary>
    Task<IReadOnlyList<SavedView>> ListForUserAsync(Guid userId, string listPage, CancellationToken cancellationToken = default);

    /// <summary>All shared (tenant) views across every list page — for the admin management page.</summary>
    Task<IReadOnlyList<SavedView>> ListSharedAsync(CancellationToken cancellationToken = default);
}
