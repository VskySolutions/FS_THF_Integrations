namespace IntegrationHub.Application.Abstractions.Security;

/// <summary>
/// Provides the current server-side <c>tokenVersion</c> for a user, used by the JWT
/// handler to reject tokens invalidated by password change or deactivation
/// (Authentication &amp; Security ADR-001). The full user store is delivered in
/// WO-38/WO-39; until then a placeholder implementation returns <c>null</c>
/// (no version enforcement).
/// </summary>
public interface IUserSecurityStore
{
    /// <summary>
    /// Returns the current token version for the user, or <c>null</c> if the user is
    /// unknown or version enforcement is not yet available.
    /// </summary>
    Task<int?> GetCurrentTokenVersionAsync(Guid userId, CancellationToken cancellationToken = default);
}
