using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using IntegrationHub.Application.Abstractions.Security;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Shared.Configuration;
using IntegrationHub.Shared.Security;
using Microsoft.Extensions.Options;

namespace IntegrationHub.Infrastructure.Security;

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
}
