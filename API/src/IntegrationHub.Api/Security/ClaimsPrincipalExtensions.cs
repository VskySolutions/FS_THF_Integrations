using System.Security.Claims;
using IntegrationHub.Shared.Security;

namespace IntegrationHub.Api.Security;

/// <summary>Convenience accessors for the platform JWT claims on the current principal.</summary>
public static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirst(ClaimTypeNames.Subject)?.Value, out var id) ? id : null;

    public static Guid? GetActiveTenantId(this ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirst(ClaimTypeNames.ActiveTenantId)?.Value, out var id) ? id : null;

    public static string? GetRole(this ClaimsPrincipal principal)
        => principal.FindFirst(ClaimTypeNames.Role)?.Value;

    public static bool IsSuperAdmin(this ClaimsPrincipal principal)
        => string.Equals(principal.GetRole(), Roles.SuperAdmin, StringComparison.Ordinal);
}
