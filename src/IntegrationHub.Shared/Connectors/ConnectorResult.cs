namespace IntegrationHub.Shared.Connectors;

/// <summary>
/// Normalized result of an <c>IConnector</c> operation. Abstracts system-specific error
/// formats from the calling service (Connector Framework contract). <see cref="IsRetriable"/>
/// drives the retry framework: transient failures retry, validation/permanent failures do not.
/// </summary>
public sealed class ConnectorResult<T>
{
    public bool Success { get; init; }

    public T? Payload { get; init; }

    public string? ErrorMessage { get; init; }

    public bool IsRetriable { get; init; }

    public static ConnectorResult<T> Ok(T payload)
        => new() { Success = true, Payload = payload };

    /// <summary>Builds a failure result. Transient/transport errors should set <paramref name="isRetriable"/> true.</summary>
    public static ConnectorResult<T> Fail(string errorMessage, bool isRetriable)
        => new() { Success = false, ErrorMessage = errorMessage, IsRetriable = isRetriable };
}
