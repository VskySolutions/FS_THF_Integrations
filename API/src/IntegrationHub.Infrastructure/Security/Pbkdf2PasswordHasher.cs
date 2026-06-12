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
        // A standard-length (16) strong password: guaranteed lower/upper/digit/special, with
        // ambiguous characters (0/O/1/l/I) omitted for readability. 16 chars keeps it within the
        // change-password form's field limit while remaining strong.
        const string lower = "abcdefghijkmnopqrstuvwxyz";   // no 'l'
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";     // no 'I', 'O'
        const string digits = "23456789";                    // no '0', '1'
        const string special = "!@#$%^&*-_?";
        const string all = lower + upper + digits + special;
        const int length = 16;

        var chars = new char[length];
        chars[0] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        chars[1] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        chars[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        chars[3] = special[RandomNumberGenerator.GetInt32(special.Length)];
        for (var i = 4; i < length; i++)
        {
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        // Fisher–Yates shuffle so the guaranteed characters aren't always in the first positions.
        for (var i = length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }
}
