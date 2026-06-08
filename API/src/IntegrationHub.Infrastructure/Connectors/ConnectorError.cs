using System.Net;
using IntegrationHub.Shared.Connectors;

namespace IntegrationHub.Infrastructure.Connectors;

/// <summary>
/// Normalizes transport-level failures into <see cref="ConnectorResult{T}"/> with a correct
/// <c>IsRetriable</c> flag (AC-COF-001.3). Transient conditions (network errors, timeouts,
/// 5xx, 408, 429) are retriable; other 4xx responses are not.
/// </summary>
internal static class ConnectorError
{
    public static ConnectorResult<T> FromStatusCode<T>(string system, string operation, HttpStatusCode statusCode, string? body)
    {
        var retriable = IsRetriable(statusCode);
        var message = $"{system} {operation} failed with HTTP {(int)statusCode} ({statusCode}). {Trim(body)}";
        return ConnectorResult<T>.Fail(message, retriable);
    }

    public static ConnectorResult<T> FromException<T>(string system, string operation, Exception exception)
    {
        // Network failures, DNS, connection resets, and timeouts are transient.
        var retriable = exception is HttpRequestException or TaskCanceledException or TimeoutException;
        var message = $"{system} {operation} failed: {exception.Message}";
        return ConnectorResult<T>.Fail(message, retriable);
    }

    private static bool IsRetriable(HttpStatusCode statusCode)
        => (int)statusCode >= 500
            || statusCode == HttpStatusCode.RequestTimeout
            || statusCode == HttpStatusCode.TooManyRequests;

    private static string Trim(string? body)
        => string.IsNullOrWhiteSpace(body) ? string.Empty : body.Length <= 500 ? body : body[..500];
}
