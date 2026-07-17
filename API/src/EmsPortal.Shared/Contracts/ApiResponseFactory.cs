using FluentValidation.Results;

namespace EmsPortal.Shared.Contracts;

/// <summary>
/// Single factory for all API responses (ADR-002 Custom Envelope Pattern). Every
/// controller and middleware produces responses through these methods.
/// </summary>
public static class ApiResponseFactory
{
    public static ApiResponse<T> Success<T>(T data, string message)
        => new() { Success = true, Message = message, Data = data };

    public static ApiResponse<IEnumerable<T>> Paginated<T>(
        IEnumerable<T> data, string message, int page, int limit, int total)
        => new()
        {
            Success = true,
            Message = message,
            Data = data,
            Meta = new PaginatedMeta { Page = page, Limit = limit, TotalRecords = total },
        };

    public static ApiErrorResponse Error(string code, string message, string details)
        => new()
        {
            Success = false,
            Message = message,
            Error = new ApiError { Code = code, Details = details },
        };

    /// <summary>Builds a VALIDATION_FAILED envelope with per-field details.</summary>
    public static ApiErrorResponse ValidationError(IEnumerable<ValidationFailure> failures)
    {
        var details = string.Join("; ", failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}"));
        return Error(ApiErrorCodes.ValidationFailed, "One or more validation errors occurred.", details);
    }

    public static ApiErrorResponse NotFound(string details)
        => Error(ApiErrorCodes.NotFound, "Resource not found.", details);

    public static ApiErrorResponse Unauthorized(string details)
        => Error(ApiErrorCodes.Unauthorized, "Unauthorized.", details);

    public static ApiErrorResponse Forbidden(string details)
        => Error(ApiErrorCodes.Forbidden, "Forbidden.", details);
}
