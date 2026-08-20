using EmsPortal.Domain.Entities;

namespace EmsPortal.Application.Abstractions.Persistence;

/// <summary>Data access for RBAC roles and their tenant assignments.</summary>
public interface IRoleRepository
{
    /// <summary>Every role on the platform, whoever owns it. Super-Admin surfaces only.</summary>
    Task<IReadOnlyList<Role>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The roles a tenant may see: the platform roles plus the ones that tenant owns. This is the list
    /// behind every non-Super-Admin role surface — another tenant's roles are not in it.
    /// </summary>
    Task<IReadOnlyList<Role>> ListVisibleToTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The PLATFORM role of that name (seeded system roles and anything else a Super Admin created).
    /// Tenant-owned roles are deliberately out of reach here: their names repeat across tenants, so a
    /// lookup by name alone has no answer — and every caller of this (the seeder, the legacy
    /// role-name fallback) is asking about a platform role.
    /// </summary>
    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether <paramref name="name"/> is already taken for a role owned by <paramref name="tenantId"/>
    /// (<c>null</c> = the platform scope). A tenant clashes with its own roles and with the platform ones
    /// it inherits; a platform name must be free everywhere, since it would otherwise appear beside a
    /// same-named tenant role in that tenant's list.
    /// </summary>
    Task<bool> NameExistsAsync(string name, Guid? tenantId, Guid? excludeRoleId = null, CancellationToken cancellationToken = default);

    Task AddAsync(Role role, CancellationToken cancellationToken = default);

    void Update(Role role);

    void Remove(Role role);

    /// <summary>
    /// Roles a tenant may assign: those made available to it via <see cref="TenantRole"/>, plus the ones
    /// it owns. Feeds the permission ceiling — what a tenant's own roles already grant is by definition
    /// within reach of its admins.
    /// </summary>
    Task<IReadOnlyList<Role>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Tenant ids a role is currently available to (via <see cref="TenantRole"/>).</summary>
    Task<IReadOnlyList<Guid>> ListTenantIdsForRoleAsync(Guid roleId, CancellationToken cancellationToken = default);

    Task<TenantRole?> GetTenantRoleAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken = default);

    Task AddTenantRoleAsync(TenantRole tenantRole, CancellationToken cancellationToken = default);

    void RemoveTenantRole(TenantRole tenantRole);
}
