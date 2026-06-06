namespace IntegrationHub.Application.Abstractions.Security;

/// <summary>
/// Validates a JWT's <c>tokenVersion</c> claim against the current server-side value for
/// the user, providing immediate session invalidation on password change or deactivation
/// (Authentication &amp; Security ADR-001).
/// <para>
/// Phase 1 ships a skeleton that returns <c>true</c> for every token. The DB-backed
/// comparison against the <c>User</c> record is wired in WO-38 once <c>UserRepository</c>
/// exists.
/// </para>
/// </summary>
public interface ITokenVersionValidator
{
    /// <summary>
    /// Returns <c>true</c> when the presented <paramref name="tokenVersion"/> is still
    /// valid for the user; <c>false</c> rejects the token like an expired one.
    /// </summary>
    Task<bool> IsValidAsync(Guid userId, int tokenVersion, CancellationToken cancellationToken = default);
}
