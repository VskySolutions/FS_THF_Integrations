using IntegrationHub.Domain.Entities;

namespace IntegrationHub.Application.Abstractions.Persistence;

/// <summary>
/// Data access for <see cref="Tenant"/> records. Tenant lookup is not itself tenant-scoped.
/// </summary>
public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Tenant?> GetByIdentifierAsync(string identifier, CancellationToken cancellationToken = default);

    Task<bool> IdentifierExistsAsync(string identifier, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default);

    void Update(Tenant tenant);
}
