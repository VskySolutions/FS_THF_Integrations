using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Application.Abstractions.Persistence;

/// <summary>Data access for tenant <see cref="Tag"/>s and their <see cref="EntityTag"/> applications.</summary>
public interface ITagRepository
{
    Task AddAsync(Tag tag, CancellationToken cancellationToken = default);

    Task<Tag?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Tag?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    void Update(Tag tag);

    void Remove(Tag tag);

    Task<IReadOnlyList<Tag>> ListAsync(string? search, CancellationToken cancellationToken = default);

    /// <summary>Usage counts (applied <see cref="EntityTag"/>s) keyed by tag id.</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetUsageCountsAsync(CancellationToken cancellationToken = default);

    // ---- Entity tags (applications) ----
    Task<IReadOnlyList<EntityTag>> GetEntityTagsAsync(EntityType entityType, Guid entityId, CancellationToken cancellationToken = default);

    Task<EntityTag?> GetEntityTagAsync(Guid id, CancellationToken cancellationToken = default);

    Task<EntityTag?> GetEntityTagAsync(EntityType entityType, Guid entityId, Guid tagId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EntityTag>> GetApplicationsByTagAsync(Guid tagId, CancellationToken cancellationToken = default);

    Task AddEntityTagAsync(EntityTag entityTag, CancellationToken cancellationToken = default);

    void RemoveEntityTag(EntityTag entityTag);
}
