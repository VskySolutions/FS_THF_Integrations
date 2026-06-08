namespace IntegrationHub.Shared.Security;

/// <summary>
/// JWT claim names carried by platform-issued tokens. Inbound claim mapping is
/// disabled so these raw names are preserved on the principal.
/// </summary>
public static class ClaimTypeNames
{
    public const string Subject = "sub";
    public const string ActiveTenantId = "activeTenantId";
    public const string Role = "role";
    public const string TokenVersion = "tokenVersion";
    public const string TenantAssignments = "tenantAssignments";
}
