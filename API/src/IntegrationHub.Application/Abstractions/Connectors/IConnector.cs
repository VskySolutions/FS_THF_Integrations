using IntegrationHub.Domain.Enums;
using IntegrationHub.Shared.Connectors;

namespace IntegrationHub.Application.Abstractions.Connectors;

/// <summary>
/// Contract for authenticating with and communicating with a single external system
/// (REQ-COF-001). Implementations are stateless between calls; auth tokens may be cached
/// in-memory per instance but never persisted. All methods return a normalized
/// <see cref="ConnectorResult{T}"/>, hiding system-specific error formats from callers.
/// </summary>
/// <typeparam name="TFetch">Payload type returned by fetch operations.</typeparam>
/// <typeparam name="TPush">Payload type accepted by push operations.</typeparam>
public interface IConnector<TFetch, TPush>
{
    /// <summary>The external system this connector targets.</summary>
    SystemName System { get; }

    /// <summary>Authenticates with the external system (acquiring/refreshing a token).</summary>
    Task<ConnectorResult<bool>> AuthenticateAsync(CancellationToken cancellationToken = default);

    /// <summary>Fetches records from the source system.</summary>
    Task<ConnectorResult<TFetch>> FetchAsync(CancellationToken cancellationToken = default);

    /// <summary>Pushes a record to the target system.</summary>
    Task<ConnectorResult<bool>> PushAsync(TPush payload, CancellationToken cancellationToken = default);
}
