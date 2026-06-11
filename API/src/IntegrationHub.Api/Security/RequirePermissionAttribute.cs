using Microsoft.AspNetCore.Authorization;

namespace IntegrationHub.Api.Security;

/// <summary>
/// Requires the caller to hold the given permission (see <see cref="Shared.Security.Permissions"/>).
/// Backed by a dynamically-materialized policy resolved by <see cref="PermissionPolicyProvider"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission)
        => Policy = PermissionAuthorizationDefaults.PolicyPrefix + permission;
}
