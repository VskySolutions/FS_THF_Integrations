namespace IntegrationHub.Api.Contracts;

/// <summary>
/// Builds <see cref="ApiResponse"/> envelopes (ADR-002). The dedicated ApiResponseFactory
/// work order expands this with the success/paged helpers; the error path required by
/// WO-7 is provided here.
/// </summary>
public static class ApiResponseFactory
{
    /// <summary>Builds a failure envelope with the given message, error code, and detail.</summary>
    public static ApiResponse Error(string message, string code, string? details = null)
        => new()
        {
            Success = false,
            Message = message,
            Error = new ApiError { Code = code, Details = details },
        };

    /// <summary>Builds a success envelope carrying a typed payload.</summary>
    public static ApiResponse<T> Success<T>(T data, string? message = null)
        => new()
        {
            Success = true,
            Message = message,
            Data = data,
        };
}
