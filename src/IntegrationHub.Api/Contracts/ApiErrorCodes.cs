namespace IntegrationHub.Api.Contracts;

/// <summary>Stable machine-readable error codes used in <see cref="ApiError.Code"/>.</summary>
public static class ApiErrorCodes
{
    /// <summary>Unhandled server-side failure.</summary>
    public const string InternalError = "INTERNAL_ERROR";

    /// <summary>Request failed model/business validation.</summary>
    public const string ValidationFailed = "VALIDATION_FAILED";
}
