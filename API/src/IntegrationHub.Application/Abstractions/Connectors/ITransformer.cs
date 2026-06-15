using IntegrationHub.Domain.Enums;
using IntegrationHub.Shared.Connectors;

namespace IntegrationHub.Application.Abstractions.Connectors;

/// <summary>
/// Contract for converting a payload from one system's schema to another (REQ-COF-002).
/// Implementations apply active <c>MappingConfiguration</c> records for the
/// (source, destination) system pair at invocation time.
/// </summary>
public interface ITransformer<TSource, TDest>
{
    /// <summary>Source system of the payload being transformed.</summary>
    SystemName SourceSystem { get; }

    /// <summary>Destination system of the mapped payload.</summary>
    SystemName DestinationSystem { get; }

    /// <summary>
    /// Transforms a source payload into the destination schema, applying the tenant's active field
    /// rules for the (source, destination) pair + <paramref name="interfaceName"/> flow.
    /// </summary>
    Task<TransformResult<TDest>> TransformAsync(TSource source, string interfaceName, CancellationToken cancellationToken = default);
}
