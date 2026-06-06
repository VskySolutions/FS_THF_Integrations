using IntegrationHub.Domain.Entities;

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

    Task<IReadOnlyList<MappingConfiguration>> ListAsync(CancellationToken cancellationToken = default);

    Task AddAsync(MappingConfiguration configuration, CancellationToken cancellationToken = default);

    void Update(MappingConfiguration configuration);
}
