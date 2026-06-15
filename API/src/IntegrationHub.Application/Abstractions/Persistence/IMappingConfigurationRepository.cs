using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Application.Abstractions.Persistence;

/// <summary>
/// Data access for <see cref="MappingConfiguration"/> field rules, scoped per tenant + flow.
/// Written by the Admin API and read by the Background Worker on every transformer invocation.
/// </summary>
public interface IMappingConfigurationRepository
{
    Task<MappingConfiguration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Active field rules for a (source, destination) pair + flow within the ambient tenant —
    /// the transformer's runtime resolution. Read fresh on every invocation (no caching).
    /// </summary>
    Task<IReadOnlyList<MappingConfiguration>> GetActiveForFlowAsync(
        SystemName sourceSystem, SystemName destinationSystem, string interfaceName, CancellationToken cancellationToken = default);

    /// <summary>All (non-deleted) field rules for a tenant + flow (admin management; ignores the ambient tenant filter).</summary>
    Task<IReadOnlyList<MappingConfiguration>> ListByTenantFlowAsync(
        Guid tenantId, string interfaceName, CancellationToken cancellationToken = default);

    Task AddAsync(MappingConfiguration configuration, CancellationToken cancellationToken = default);

    void Update(MappingConfiguration configuration);

    void Remove(MappingConfiguration configuration);
}
