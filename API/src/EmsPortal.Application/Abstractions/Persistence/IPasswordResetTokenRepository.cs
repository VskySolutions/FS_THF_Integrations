using EmsPortal.Domain.Entities;

namespace EmsPortal.Application.Abstractions.Persistence;

/// <summary>
/// Data access for the self-service password-reset tokens. Not tenant-scoped — the flow is anonymous.
/// </summary>
public interface IPasswordResetTokenRepository
{
    /// <summary>The token with this hash, redeemed or not. Callers check expiry/use themselves.</summary>
    Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken = default);

    void Update(PasswordResetToken token);

    /// <summary>
    /// Marks every unredeemed token for a user as used. Called when a new one is issued and again when one
    /// is redeemed, so at most one live token exists per account.
    /// </summary>
    Task InvalidateAllForUserAsync(Guid userId, DateTime onUtc, CancellationToken cancellationToken = default);
}
