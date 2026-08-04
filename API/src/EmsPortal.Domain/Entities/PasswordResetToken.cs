namespace EmsPortal.Domain.Entities;

/// <summary>
/// A one-time token backing the self-service "forgot password" flow. Deliberately NOT tenant-scoped: the
/// whole flow is anonymous — the person asking has not signed in, so there is no tenant context to filter
/// by — and a user may hold assignments in several tenants anyway.
/// <para>
/// Only the SHA-256 hash of the token is stored, exactly as <see cref="RefreshToken"/> does: the plaintext
/// exists only in the emailed link, so a database leak cannot be replayed into account takeover.
/// </para>
/// </summary>
public class PasswordResetToken : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>The account the token resets.</summary>
    public Guid UserId { get; set; }

    /// <summary>SHA-256 of the emailed token. The plaintext is never persisted.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>When the token stops being accepted.</summary>
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>When it was redeemed. Non-null means spent — a token is good for exactly one reset.</summary>
    public DateTime? UsedOnUtc { get; set; }

    // ---- Navigations ----
    public User? User { get; set; }
}
