using EmsPortal.Domain.Entities;

namespace EmsPortal.Application.Abstractions.Persistence;

/// <summary>
/// Data access for <see cref="User"/> and <see cref="UserTenantRole"/>. User lookups are
/// not tenant-filtered; tenant scoping for listings is applied via the optional tenant id.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Resolves user ids to display full names (FirstName + LastName), for Created/Updated By columns.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetFullNamesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Paginated user list. When <paramref name="tenantId"/> is set, only that tenant's users;
    /// optional <paramref name="search"/> (email/name), <paramref name="isActive"/>, and per-column
    /// <paramref name="name"/>/<paramref name="email"/>/<paramref name="phone"/>/<paramref name="role"/>
    /// (role name) filters are applied server-side so pagination/totals reflect the filtered set.
    /// </summary>
    Task<(IReadOnlyList<User> Items, int Total)> ListAsync(
        Guid? tenantId, string? search, bool? isActive,
        string? name, string? email, string? phone, string? role, string? group,
        int page, int limit, CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);

    void Update(User user);

    Task<UserTenantRole?> GetAssignmentAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

    Task AddAssignmentAsync(UserTenantRole assignment, CancellationToken cancellationToken = default);

    void RemoveAssignment(UserTenantRole assignment);
}
