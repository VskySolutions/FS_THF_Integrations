using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Application.Abstractions.Persistence;

/// <summary>
/// Data access for <see cref="MappingConfiguration"/> records. Written by the Admin
/// API and read by the Background Worker on every transformer invocation.
/// </summary>
public interface IMappingConfigurationRepository
{
    Task<MappingConfiguration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns the active configuration for an interface, if any.</summary>
    Task<MappingConfiguration?> GetActiveAsync(string interfaceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all active field mapping rules for a (source, destination) system pair.
    /// Read fresh on every transformer invocation — no caching (AC-COF-005.2).
    /// </summary>
    Task<IReadOnlyList<MappingConfiguration>> GetActiveByPairAsync(
        SystemName sourceSystem,
        SystemName destinationSystem,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MappingConfiguration>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Paginated mappings for a specific tenant (admin management; ignores the ambient tenant filter).</summary>
    Task<(IReadOnlyList<MappingConfiguration> Items, int Total)> ListByTenantAsync(
        Guid tenantId, int page, int limit, CancellationToken cancellationToken = default);

    /// <summary>Fetches a mapping by id within a specific tenant (admin management).</summary>
    Task<MappingConfiguration?> GetByIdForTenantAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Finds the active mapping for a tenant's (source, destination, sourceField), for replace-on-create.</summary>
    Task<MappingConfiguration?> GetActiveForFieldAsync(
        Guid tenantId, SystemName sourceSystem, SystemName destinationSystem, string sourceField, CancellationToken cancellationToken = default);

    Task AddAsync(MappingConfiguration configuration, CancellationToken cancellationToken = default);

    void Update(MappingConfiguration configuration);

    void Remove(MappingConfiguration configuration);
}
