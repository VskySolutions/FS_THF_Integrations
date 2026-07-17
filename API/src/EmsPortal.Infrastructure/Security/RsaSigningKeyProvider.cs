using System.Security.Cryptography;
using EmsPortal.Shared.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EmsPortal.Infrastructure.Security;

/// <summary>
/// RSA signing-key provider. Loads the private key from <c>Authentication:PrivateKeyPem</c>
/// when configured; otherwise generates an ephemeral 2048-bit key for the process lifetime
/// (development). Registered as a singleton so the key is stable across requests.
/// </summary>
internal sealed class RsaSigningKeyProvider : ISigningKeyProvider
{
    private readonly RsaSecurityKey _key;

    public RsaSigningKeyProvider(IOptions<AuthenticationOptions> options)
    {
        var rsa = RSA.Create(2048);
        var pem = options.Value.PrivateKeyPem;
        if (!string.IsNullOrWhiteSpace(pem))
        {
            rsa.ImportFromPem(pem);
        }

        _key = new RsaSecurityKey(rsa) { KeyId = "integrationhub-signing-key" };
        SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.RsaSha256);
    }

    public SecurityKey ValidationKey => _key;

    public SigningCredentials SigningCredentials { get; }
}
