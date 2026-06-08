using IntegrationHub.Domain.Entities;

namespace IntegrationHub.Application.Abstractions.Persistence;

/// <summary>
/// Data access for <see cref="User"/> and <see cref="UserTenantRole"/>. User lookups are
/// not tenant-filtered; tenant scoping for listings is applied via the optional tenant id.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Paginated user list; when <paramref name="tenantId"/> is set, only that tenant's users.</summary>
    Task<(IReadOnlyList<User> Items, int Total)> ListAsync(Guid? tenantId, int page, int limit, CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);

    void Update(User user);

    Task<UserTenantRole?> GetAssignmentAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

    Task AddAssignmentAsync(UserTenantRole assignment, CancellationToken cancellationToken = default);

    void RemoveAssignment(UserTenantRole assignment);
}
