using IntegrationHub.Application.Abstractions.Security;

namespace IntegrationHub.Infrastructure.Security;

/// <summary>
/// Placeholder user security store used until the user/role management features
/// (WO-38/WO-39) provide a database-backed store. Returns <c>null</c>, which the JWT
/// handler treats as "no token-version enforcement", so valid tokens are accepted.
/// </summary>
internal sealed class PlaceholderUserSecurityStore : IUserSecurityStore
{
    public Task<int?> GetCurrentTokenVersionAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult<int?>(null);
}
