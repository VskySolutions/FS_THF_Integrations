using System.Security.Cryptography;
using IntegrationHub.Application.Abstractions.Security;
using IntegrationHub.Shared.Configuration;
using Microsoft.Extensions.Options;

namespace IntegrationHub.Infrastructure.Security;

/// <summary>
/// Validates API keys against PBKDF2 (SHA-256) hashes loaded from configuration.
/// Comparison is constant-time. When the user/role features land, this can be
/// swapped for a database-backed validator without touching the handler.
/// </summary>
internal sealed class Pbkdf2ApiKeyValidator : IApiKeyValidator
{
    private const int Iterations = 100_000;
    private const int HashByteLength = 32;

    private readonly IOptionsMonitor<ApiKeysOptions> _options;

    public Pbkdf2ApiKeyValidator(IOptionsMonitor<ApiKeysOptions> options)
    {
        _options = options;
    }

    public Task<ApiKeyPrincipal?> ValidateAsync(string presentedApiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(presentedApiKey))
        {
            return Task.FromResult<ApiKeyPrincipal?>(null);
        }

        foreach (var entry in _options.CurrentValue.Keys)
        {
            if (string.IsNullOrEmpty(entry.Salt) || string.IsNullOrEmpty(entry.Hash))
            {
                continue;
            }

            byte[] salt;
            byte[] expected;
            try
            {
                salt = Convert.FromBase64String(entry.Salt);
                expected = Convert.FromBase64String(entry.Hash);
            }
            catch (FormatException)
            {
                continue;
            }

            var actual = Rfc2898DeriveBytes.Pbkdf2(presentedApiKey, salt, Iterations, HashAlgorithmName.SHA256, HashByteLength);
            if (CryptographicOperations.FixedTimeEquals(actual, expected))
            {
                return Task.FromResult<ApiKeyPrincipal?>(new ApiKeyPrincipal(entry.Name, entry.Role, entry.TenantId));
            }
        }

        return Task.FromResult<ApiKeyPrincipal?>(null);
    }
}
