using EmsPortal.Domain.Entities;

namespace EmsPortal.Application.Abstractions.Persistence;

/// <summary>Data access for RBAC roles and their tenant assignments.</summary>
public interface IRoleRepository
{
    Task<IReadOnlyList<Role>> ListAsync(CancellationToken cancellationToken = default);

    Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken = default);

    Task AddAsync(Role role, CancellationToken cancellationToken = default);

    void Update(Role role);

    void Remove(Role role);

    /// <summary>Roles made available to a tenant (via <see cref="TenantRole"/>).</summary>
    Task<IReadOnlyList<Role>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Tenant ids a role is currently available to (via <see cref="TenantRole"/>).</summary>
    Task<IReadOnlyList<Guid>> ListTenantIdsForRoleAsync(Guid roleId, CancellationToken cancellationToken = default);

    Task<TenantRole?> GetTenantRoleAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken = default);

    Task AddTenantRoleAsync(TenantRole tenantRole, CancellationToken cancellationToken = default);

    void RemoveTenantRole(TenantRole tenantRole);
}
