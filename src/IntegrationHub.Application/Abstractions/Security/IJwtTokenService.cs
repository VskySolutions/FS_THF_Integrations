using IntegrationHub.Domain.Entities;

namespace IntegrationHub.Application.Abstractions.Security;

/// <summary>Issues short-lived RS256 access tokens for authenticated users.</summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Builds an access token for the user scoped to <paramref name="activeTenantId"/>,
    /// embedding sub/email/role/tokenVersion/activeTenantId/tenantAssignments claims.
    /// </summary>
    AccessToken CreateAccessToken(User user, Guid activeTenantId);
}

/// <summary>An issued access token and its lifetime.</summary>
public sealed record AccessToken(string Token, int ExpiresInSeconds);
