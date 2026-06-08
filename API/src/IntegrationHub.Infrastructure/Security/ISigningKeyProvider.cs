using Microsoft.IdentityModel.Tokens;

namespace IntegrationHub.Infrastructure.Security;

/// <summary>
/// Supplies the platform's RS256 signing material. The same RSA key signs issued JWTs and
/// validates them, so issuance (AuthController) and validation (JwtBearer) always agree.
/// </summary>
public interface ISigningKeyProvider
{
    /// <summary>Validation key.</summary>
    SecurityKey ValidationKey { get; }

    /// <summary>Credentials used to sign issued tokens.</summary>
    SigningCredentials SigningCredentials { get; }
}
