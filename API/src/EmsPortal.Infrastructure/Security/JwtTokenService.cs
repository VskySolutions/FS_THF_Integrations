using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using EmsPortal.Application.Abstractions.Security;
using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;
using EmsPortal.Shared.Configuration;
using EmsPortal.Shared.Security;
using Microsoft.Extensions.Options;

namespace EmsPortal.Infrastructure.Security;

/// <summary>
/// Issues RS256 access tokens carrying sub/email/activeTenantId/role/tokenVersion and a
/// serialized tenantAssignments array (Admin User &amp; Role Management). The role claim is
/// the user's role in the active tenant; a Super Admin assignment grants SuperAdmin in any
/// tenant (AC-ADM-008.6).
/// </summary>
internal sealed class JwtTokenService : IJwtTokenService
{
    private readonly ISigningKeyProvider _signingKeyProvider;
    private readonly AuthenticationOptions _options;

    public JwtTokenService(ISigningKeyProvider signingKeyProvider, IOptions<AuthenticationOptions> options)
    {
        _signingKeyProvider = signingKeyProvider;
        _options = options.Value;
    }

    public AccessToken CreateAccessToken(User user, Guid activeTenantId)
    {
        var role = ResolveRole(user, activeTenantId);
        var assignments = user.TenantRoles
            .Select(r => new { tenantId = r.TenantId, role = r.Role.ToString() })
            .ToArray();

        var claims = new List<Claim>
        {
            new(ClaimTypeNames.Subject, user.Id.ToString()),
            new("email", user.Email),
            new(ClaimTypeNames.ActiveTenantId, activeTenantId.ToString()),
            new(ClaimTypeNames.TokenVersion, user.TokenVersion.ToString()),
            new(ClaimTypeNames.TenantAssignments, JsonSerializer.Serialize(assignments)),
        };
        if (role is not null)
        {
            claims.Add(new Claim(ClaimTypeNames.Role, role));
        }

        // Effective permissions for the active tenant — the union of the assigned roles' permission
        // sets — drive permission-based authorization and the client's permission-gated UI.
        foreach (var permission in ResolvePermissions(user, activeTenantId))
        {
            claims.Add(new Claim(ClaimTypeNames.Permission, permission));
        }

        var expires = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes <= 0 ? 60 : _options.AccessTokenMinutes);
        var token = new JwtSecurityToken(
            issuer: string.IsNullOrWhiteSpace(_options.Issuer) ? null : _options.Issuer,
            audience: string.IsNullOrWhiteSpace(_options.Audience) ? null : _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: _signingKeyProvider.SigningCredentials);

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);
        var expiresIn = (int)(expires - DateTime.UtcNow).TotalSeconds;
        return new AccessToken(encoded, expiresIn);
    }

    private static string? ResolveRole(User user, Guid activeTenantId)
    {
        if (user.TenantRoles.Any(r => r.Role == UserRole.SuperAdmin))
        {
            return Roles.SuperAdmin;
        }

        var assignment = user.TenantRoles.FirstOrDefault(r => r.TenantId == activeTenantId);
        return assignment?.Role.ToString();
    }

    /// <summary>
    /// The user's effective permissions in the active tenant: the full catalogue for a Super Admin
    /// (assigned anywhere), otherwise the active tenant assignment's RBAC role permissions — the union
    /// of the role's direct keys and the group-derived cache (<see cref="Role.EffectivePermissions"/>,
    /// Permission Groups feature) — falling back to the seeded set for the legacy enum when the role
    /// carries no keys from either source.
    /// </summary>
    private static IReadOnlyList<string> ResolvePermissions(User user, Guid activeTenantId)
    {
        if (user.TenantRoles.Any(r => r.Role == UserRole.SuperAdmin))
        {
            return Permissions.ForSuperAdmin();
        }

        var assignment = user.TenantRoles.FirstOrDefault(r => r.TenantId == activeTenantId);
        if (assignment is null)
        {
            return Array.Empty<string>();
        }

        if (assignment.RoleEntity is { } roleEntity)
        {
            // Direct role permissions ∪ permissions contributed by composed Permission Groups.
            var effective = roleEntity.Permissions
                .Concat(roleEntity.EffectivePermissions)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (effective.Count > 0)
            {
                return effective;
            }
        }

        return Permissions.ForSystemRole(assignment.Role.ToString());
    }
}
