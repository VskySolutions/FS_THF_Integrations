namespace EmsPortal.Shared.Security;

/// <summary>
/// Names of the three named RBAC authorization policies (REQ-ADM-005).
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Permits only SuperAdmin.</summary>
    public const string SuperAdminOnly = "SuperAdminOnly";

    /// <summary>Permits SuperAdmin and TenantAdmin.</summary>
    public const string TenantAdminOrAbove = "TenantAdminOrAbove";

    /// <summary>Permits SuperAdmin, TenantAdmin, and Operator.</summary>
    public const string OperatorOrAbove = "OperatorOrAbove";
}
