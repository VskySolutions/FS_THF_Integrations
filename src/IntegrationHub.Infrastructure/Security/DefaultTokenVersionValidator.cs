using IntegrationHub.Application.Abstractions.Security;

namespace IntegrationHub.Infrastructure.Security;

/// <summary>
/// Phase 1 token-version skeleton: accepts every token version. WO-38 (Phase 4)
/// replaces this with a <c>UserRepository</c>-backed comparison against the stored
/// <c>tokenVersion</c> on the <c>User</c> record.
/// </summary>
internal sealed class DefaultTokenVersionValidator : ITokenVersionValidator
{
    public Task<bool> IsValidAsync(Guid userId, int tokenVersion, CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}
