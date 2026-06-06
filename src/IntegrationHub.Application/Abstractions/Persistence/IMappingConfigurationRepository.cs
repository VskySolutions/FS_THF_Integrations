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

    Task AddAsync(MappingConfiguration configuration, CancellationToken cancellationToken = default);

    void Update(MappingConfiguration configuration);
}
