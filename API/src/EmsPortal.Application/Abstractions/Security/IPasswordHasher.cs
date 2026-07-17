namespace EmsPortal.Application.Abstractions.Security;

/// <summary>
/// Hashes and verifies passwords with PBKDF2-SHA256 (≥100,000 iterations, per-user salt).
/// Plaintext passwords are never persisted or logged.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Hashes a password, returning base64 hash and salt.</summary>
    (string Hash, string Salt) Hash(string password);

    /// <summary>Verifies a password against a stored base64 hash and salt.</summary>
    bool Verify(string password, string hash, string salt);

    /// <summary>Generates a random temporary password for a new account.</summary>
    string GenerateTemporaryPassword();
}
