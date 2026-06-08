namespace IntegrationHub.Domain.Entities;

/// <summary>
/// A long-lived refresh token (stored hashed). Rotated on refresh; revoked on logout,
/// password change, and deactivation (Admin User &amp; Role Management).
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>SHA-256 hash of the token; the plaintext is returned only to the caller.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public bool IsRevoked { get; set; }

    public DateTime CreatedAt { get; set; }
}
