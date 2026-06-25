using System.Security.Claims;
using IntegrationHub.Shared.Security;
using Microsoft.AspNetCore.Authorization;

namespace IntegrationHub.Api.Security;

/// <summary>Shared constants for permission-based authorization policies.</summary>
public static class PermissionAuthorizationDefaults
{
    /// <summary>Prefix for the dynamically-materialized per-permission policies (e.g. "perm:tenants.write").</summary>
    public const string PolicyPrefix = "perm:";
}

/// <summary>
/// An authorization requirement satisfied when the caller holds ANY of the listed permission keys
/// (see <see cref="Permissions"/>). A single permission is the common case; multiple express an OR.
/// </summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(params string[] permissions) => Permissions = permissions;

    public IReadOnlyList<string> Permissions { get; }
}

/// <summary>
/// Grants a <see cref="PermissionRequirement"/> when the caller carries any of its permission
/// claims. As a fallback for callers without explicit permission claims (API-key callers and
/// pre-RBAC tokens), the role claim is mapped to its seeded system-role permission set.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (requirement.Permissions.Any(permission => HasPermission(context.User, permission)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static bool HasPermission(ClaimsPrincipal user, string permission)
    {
        if (user.HasClaim(ClaimTypeNames.Permission, permission))
        {
            return true;
        }

        var role = user.FindFirst(ClaimTypeNames.Role)?.Value;
        return Permissions.ForSystemRole(role).Contains(permission);
    }
}
