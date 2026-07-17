using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmsPortal.Infrastructure.Persistence.Repositories;

internal sealed class RoleRepository : IRoleRepository
{
    private readonly EmsPortalDbContext _dbContext;

    public RoleRepository(EmsPortalDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Role>> ListAsync(CancellationToken cancellationToken = default)
        => await _dbContext.Roles.OrderByDescending(r => r.UpdatedOnUtc).ToListAsync(cancellationToken);

    public Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Roles.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        => _dbContext.Roles.FirstOrDefaultAsync(r => r.Name == name, cancellationToken);

    public Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken = default)
        => _dbContext.Roles.AnyAsync(r => r.Name == name, cancellationToken);

    public Task AddAsync(Role role, CancellationToken cancellationToken = default)
        => _dbContext.Roles.AddAsync(role, cancellationToken).AsTask();

    public void Update(Role role) => _dbContext.Roles.Update(role);

    public void Remove(Role role) => _dbContext.Roles.Remove(role);

    public async Task<IReadOnlyList<Role>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var roleIds = _dbContext.TenantRoles.Where(tr => tr.TenantId == tenantId).Select(tr => tr.RoleId);
        return await _dbContext.Roles
            .Where(r => roleIds.Contains(r.Id))
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ListTenantIdsForRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
        => await _dbContext.TenantRoles
            .Where(tr => tr.RoleId == roleId)
            .Select(tr => tr.TenantId)
            .ToListAsync(cancellationToken);

    public Task<TenantRole?> GetTenantRoleAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken = default)
        => _dbContext.TenantRoles.FirstOrDefaultAsync(tr => tr.TenantId == tenantId && tr.RoleId == roleId, cancellationToken);

    public Task AddTenantRoleAsync(TenantRole tenantRole, CancellationToken cancellationToken = default)
        => _dbContext.TenantRoles.AddAsync(tenantRole, cancellationToken).AsTask();

    public void RemoveTenantRole(TenantRole tenantRole) => _dbContext.TenantRoles.Remove(tenantRole);
}
