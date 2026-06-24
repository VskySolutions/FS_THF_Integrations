using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Application.Abstractions.Persistence;

/// <summary>Data access for a user's <see cref="Pin"/>s (bookmarks).</summary>
public interface IPinRepository
{
    Task AddAsync(Pin pin, CancellationToken cancellationToken = default);

    Task<Pin?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);

    Task<Pin?> GetAsync(Guid userId, EntityType entityType, Guid entityId, CancellationToken cancellationToken = default);

    void Remove(Pin pin);

    Task<int> CountByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Pin> Items, int Total)> ListByUserAsync(Guid userId, int page, int limit, CancellationToken cancellationToken = default);
}
