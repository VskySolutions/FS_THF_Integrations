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

    /// <summary>
    /// Active users holding an EXACT RBAC role name within a tenant (e.g. the REMS "Admin" role for the
    /// assign dropdown, WO-111). Exact match so "Admin" never also returns "TenantAdmin"/"SuperAdmin".
    /// </summary>
    Task<IReadOnlyList<User>> ListByTenantRoleAsync(Guid tenantId, string roleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Active users who can act on REMS Admin work in a tenant: those holding <paramref name="tenantRoleName"/>
    /// in that tenant, PLUS users holding <paramref name="globalRoleName"/> in ANY tenant (Super Admins are
    /// platform-wide, so their assignment may live in a different tenant). Exact role-name match; deduped.
    /// </summary>
    Task<IReadOnlyList<User>> ListByTenantRoleOrGlobalAsync(Guid tenantId, string tenantRoleName, string globalRoleName, CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);

    void Update(User user);

    /// <summary>All active (non-deleted) role assignments a user holds in a tenant (multi-role).</summary>
    Task<IReadOnlyList<UserTenantRole>> GetAssignmentsAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

    Task AddAssignmentAsync(UserTenantRole assignment, CancellationToken cancellationToken = default);

    void RemoveAssignment(UserTenantRole assignment);
}
