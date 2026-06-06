namespace IntegrationHub.Api.Contracts;

/// <summary>
/// Standard API response envelope (Integration API ADR-002). All non-exempt endpoints
/// return this shape; health endpoints are exempt and return the native format.
/// </summary>
public class ApiResponse
{
    /// <summary>Whether the request succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Human-readable summary of the outcome.</summary>
    public string? Message { get; init; }

    /// <summary>Error detail when <see cref="Success"/> is false; otherwise null.</summary>
    public ApiError? Error { get; init; }
}

/// <summary>Response envelope carrying a typed payload on success.</summary>
public sealed class ApiResponse<T> : ApiResponse
{
    public T? Data { get; init; }
}

/// <summary>Structured error block within an <see cref="ApiResponse"/>.</summary>
public sealed class ApiError
{
    /// <summary>Stable machine-readable error code (e.g. INTERNAL_ERROR, VALIDATION_FAILED).</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Caller-safe detail. For server errors this is the correlation ID.</summary>
    public string? Details { get; init; }
}
