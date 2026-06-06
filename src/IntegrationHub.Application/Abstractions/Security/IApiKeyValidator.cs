namespace IntegrationHub.Application.Abstractions.Security;

/// <summary>
/// Validates a presented API key against stored PBKDF2 hashes. Plaintext keys are
/// never persisted or logged.
/// </summary>
public interface IApiKeyValidator
{
    /// <summary>
    /// Returns the principal for a valid key, or <c>null</c> if the key is unknown.
    /// </summary>
    Task<ApiKeyPrincipal?> ValidateAsync(string presentedApiKey, CancellationToken cancellationToken = default);
}

/// <summary>The identity granted to a request authenticated by an API key.</summary>
public sealed record ApiKeyPrincipal(string Name, string Role, string? TenantId);
