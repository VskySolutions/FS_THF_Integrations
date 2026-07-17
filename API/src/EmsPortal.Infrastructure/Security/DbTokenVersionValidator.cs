using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Application.Abstractions.Security;

namespace EmsPortal.Infrastructure.Security;

/// <summary>
/// DB-backed token-version validator (WO-39): rejects tokens for unknown or inactive users,
/// or whose <c>tokenVersion</c> is below the stored value (invalidated by password change,
/// deactivation, email change, or logout). Replaces the WO-4 valid-by-default stub.
/// </summary>
internal sealed class DbTokenVersionValidator : ITokenVersionValidator
{
    private readonly IUserRepository _users;

    public DbTokenVersionValidator(IUserRepository users)
    {
        _users = users;
    }

    public async Task<bool> IsValidAsync(Guid userId, int tokenVersion, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        return user is { IsActive: true } && tokenVersion >= user.TokenVersion;
    }
}
