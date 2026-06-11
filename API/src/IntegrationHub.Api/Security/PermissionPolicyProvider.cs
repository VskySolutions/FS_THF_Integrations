using IntegrationHub.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace IntegrationHub.Api.Security;

/// <summary>
/// Materializes per-permission authorization policies on demand. A policy named
/// "perm:&lt;key&gt;" (see <see cref="RequirePermissionAttribute"/>) accepts either the JWT or
/// API-key scheme, requires an authenticated user, and adds a <see cref="PermissionRequirement"/>.
/// All other policy names fall through to the default provider (the named base-level policies and
/// the default policy registered in <c>AddAuthorization</c>).
/// </summary>
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        => _fallback = new DefaultAuthorizationPolicyProvider(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(PermissionAuthorizationDefaults.PolicyPrefix, StringComparison.Ordinal))
        {
            var permission = policyName[PermissionAuthorizationDefaults.PolicyPrefix.Length..];
            var policy = new AuthorizationPolicyBuilder(AuthenticationSchemes.Jwt, AuthenticationSchemes.ApiKey)
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }
}
