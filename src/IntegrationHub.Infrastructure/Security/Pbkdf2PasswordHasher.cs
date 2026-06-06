using System.Security.Cryptography;
using IntegrationHub.Application.Abstractions.Security;

namespace IntegrationHub.Infrastructure.Security;

/// <summary>PBKDF2-SHA256 password hasher with a 128-bit per-user salt and 100k iterations.</summary>
internal sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    public (string Hash, string Salt) Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public bool Verify(string password, string hash, string salt)
    {
        byte[] saltBytes;
        byte[] expected;
        try
        {
            saltBytes = Convert.FromBase64String(salt);
            expected = Convert.FromBase64String(hash);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, Iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public string GenerateTemporaryPassword()
    {
        // 18 random bytes → URL-safe-ish base64; guaranteed to satisfy complexity below.
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
        return $"Aa1{raw}";
    }
}
