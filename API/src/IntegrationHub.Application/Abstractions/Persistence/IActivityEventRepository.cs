using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Application.Abstractions.Persistence;

/// <summary>Data access for the append-only <see cref="ActivityEvent"/> timeline.</summary>
public interface IActivityEventRepository
{
    /// <summary>Stages a new append-only event (committed by the caller's unit of work).</summary>
    Task AddAsync(ActivityEvent activityEvent, CancellationToken cancellationToken = default);

    /// <summary>Paginated, newest-first event feed for one entity record.</summary>
    Task<(IReadOnlyList<ActivityEvent> Items, int Total)> ListAsync(
        EntityType entityType, Guid entityId, int page, int limit, CancellationToken cancellationToken = default);
}
