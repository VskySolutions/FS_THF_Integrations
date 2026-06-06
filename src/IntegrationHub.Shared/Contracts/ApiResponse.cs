namespace IntegrationHub.Shared.Contracts;

/// <summary>
/// Standard success response envelope (Integration API ADR-002). Every non-exempt
/// endpoint returns this shape via <see cref="ApiResponseFactory"/>.
/// </summary>
public sealed class ApiResponse<T>
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public T? Data { get; init; }

    /// <summary>Pagination metadata for list responses; null for single-item responses.</summary>
    public PaginatedMeta? Meta { get; init; }
}

/// <summary>Pagination metadata attached to list responses.</summary>
public sealed class PaginatedMeta
{
    public int Page { get; init; }

    public int Limit { get; init; }

    public int TotalRecords { get; init; }
}
