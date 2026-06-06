using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Application.Abstractions.Persistence;

/// <summary>
/// Data access for per-tenant external system credential configurations.
/// </summary>
public interface ITenantApiConfigurationRepository
{
    Task<TenantApiConfiguration?> GetAsync(Guid tenantId, SystemName system, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TenantApiConfiguration>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task AddAsync(TenantApiConfiguration configuration, CancellationToken cancellationToken = default);

    void Update(TenantApiConfiguration configuration);

    void Remove(TenantApiConfiguration configuration);
}
