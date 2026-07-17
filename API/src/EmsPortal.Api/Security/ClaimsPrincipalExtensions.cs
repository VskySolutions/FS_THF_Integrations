using System.Security.Claims;
using EmsPortal.Shared.Security;

namespace EmsPortal.Api.Security;

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

    /// <summary>
    /// True when the caller holds the given permission — either via an explicit permission claim or,
    /// as a fallback (API-key/pre-RBAC callers), via the seeded permission set for their system role.
    /// Mirrors <see cref="PermissionAuthorizationHandler"/>.
    /// </summary>
    public static bool HasPermission(this ClaimsPrincipal principal, string permission)
        => principal.HasClaim(ClaimTypeNames.Permission, permission)
            || Permissions.ForSystemRole(principal.GetRole()).Contains(permission);
}
