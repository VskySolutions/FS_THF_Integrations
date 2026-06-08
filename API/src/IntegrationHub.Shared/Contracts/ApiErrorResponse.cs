namespace IntegrationHub.Shared.Contracts;

/// <summary>
/// Standard error response envelope (Integration API ADR-002). <see cref="Success"/> is
/// always false; <see cref="Error"/> carries a stable machine-readable code and a
/// caller-safe detail string.
/// </summary>
public sealed class ApiErrorResponse
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public ApiError Error { get; init; } = new();
}

/// <summary>Structured error block within an <see cref="ApiErrorResponse"/>.</summary>
public sealed class ApiError
{
    public string Code { get; init; } = string.Empty;

    public string Details { get; init; } = string.Empty;
}
