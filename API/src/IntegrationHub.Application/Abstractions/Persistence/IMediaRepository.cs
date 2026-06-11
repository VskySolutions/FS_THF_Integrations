using IntegrationHub.Domain.Entities;

namespace IntegrationHub.Application.Abstractions.Persistence;

/// <summary>Data access for the centralized <see cref="Media"/> store (WO-61).</summary>
public interface IMediaRepository
{
    Task<Media?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Media media, CancellationToken cancellationToken = default);

    void Update(Media media);
}
