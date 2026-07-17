namespace IntegrationHub.Shared.Contracts;

/// <summary>Platform-wide stable error codes used in <see cref="ApiError.Code"/>.</summary>
public static class ApiErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string DuplicateIdentifier = "DUPLICATE_IDENTIFIER";
    public const string TenantInactive = "TENANT_INACTIVE";
    public const string TenantNotFound = "TENANT_NOT_FOUND";
    public const string TenantArchived = "TENANT_ARCHIVED";

    // Permission Groups
    public const string DuplicateGroupName = "DUPLICATE_GROUP_NAME";
    public const string PermissionCeilingExceeded = "PERMISSION_CEILING_EXCEEDED";
    public const string GroupInUse = "GROUP_IN_USE";

    public const string InternalError = "INTERNAL_ERROR";
}
