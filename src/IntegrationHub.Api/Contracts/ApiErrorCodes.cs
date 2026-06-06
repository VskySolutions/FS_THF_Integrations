namespace IntegrationHub.Api.Contracts;

/// <summary>Stable machine-readable error codes used in <see cref="ApiError.Code"/>.</summary>
public static class ApiErrorCodes
{
    /// <summary>Unhandled server-side failure.</summary>
    public const string InternalError = "INTERNAL_ERROR";

    /// <summary>Request failed model/business validation.</summary>
    public const string ValidationFailed = "VALIDATION_FAILED";

    /// <summary>Authentication missing or the tenant could not be resolved.</summary>
    public const string Unauthorized = "UNAUTHORIZED";

    /// <summary>The resolved tenant is inactive or archived.</summary>
    public const string TenantInactive = "TENANT_INACTIVE";

    /// <summary>The referenced tenant does not exist.</summary>
    public const string TenantNotFound = "TENANT_NOT_FOUND";
}
