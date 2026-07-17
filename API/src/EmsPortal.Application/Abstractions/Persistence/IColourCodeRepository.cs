using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;

namespace EmsPortal.Application.Abstractions.Persistence;

/// <summary>Data access for a user's <see cref="ColourCode"/> row-colour assignments.</summary>
public interface IColourCodeRepository
{
    Task AddAsync(ColourCode colourCode, CancellationToken cancellationToken = default);

    Task<ColourCode?> GetAsync(Guid userId, EntityType entityType, Guid entityId, CancellationToken cancellationToken = default);

    void Update(ColourCode colourCode);

    void Remove(ColourCode colourCode);

    /// <summary>Colour assignments for a batch of entity ids on a list page, keyed by entity id.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetBatchAsync(
        Guid userId, EntityType entityType, IReadOnlyCollection<Guid> entityIds, CancellationToken cancellationToken = default);
}
