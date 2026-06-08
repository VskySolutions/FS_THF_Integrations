namespace IntegrationHub.Shared.Security;

/// <summary>
/// The three platform roles enforced by RBAC. SuperAdmin &gt; TenantAdmin &gt; Operator.
/// </summary>
public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string TenantAdmin = "TenantAdmin";
    public const string Operator = "Operator";
}
