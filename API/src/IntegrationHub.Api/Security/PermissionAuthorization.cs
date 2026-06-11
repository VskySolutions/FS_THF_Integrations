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

/// <summary>An authorization requirement for a single permission key (see <see cref="Permissions"/>).</summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permission) => Permission = permission;

    public string Permission { get; }
}

/// <summary>
/// Grants a <see cref="PermissionRequirement"/> when the caller carries the matching permission
/// claim. As a fallback for callers without explicit permission claims (API-key callers and
/// pre-RBAC tokens), the role claim is mapped to its seeded system-role permission set.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (HasPermission(context.User, requirement.Permission))
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
