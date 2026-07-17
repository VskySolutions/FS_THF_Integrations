using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmsPortal.Infrastructure.Persistence.Repositories;

internal sealed class PermissionGroupRepository : IPermissionGroupRepository
{
    private readonly EmsPortalDbContext _dbContext;

    public PermissionGroupRepository(EmsPortalDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // ---- Groups ----

    public async Task<(IReadOnlyList<PermissionGroup> Items, int Total)> ListAsync(
        Guid? tenantId, string? search, bool? isActive, bool? usedByRoles, string? category, int page, int limit, CancellationToken cancellationToken = default)
    {
        var query = (tenantId is { } tid
            ? _dbContext.PermissionGroups.IgnoreQueryFilters().Where(g => g.TenantId == tid && !g.Deleted)
            : _dbContext.PermissionGroups.AsQueryable())
            .Include(g => g.Permissions)
            .Include(g => g.Tenant)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(g => g.Name.Contains(term) || (g.Description != null && g.Description.Contains(term)));
        }
        if (isActive is { } active)
        {
            query = query.Where(g => g.IsActive == active);
        }
        if (usedByRoles is { } used)
        {
            var linkedIds = _dbContext.RolePermissionGroups.Select(l => l.PermissionGroupId);
            query = used ? query.Where(g => linkedIds.Contains(g.Id)) : query.Where(g => !linkedIds.Contains(g.Id));
        }
        if (!string.IsNullOrWhiteSpace(category))
        {
            var prefix = category.Trim() + ".";
            query = query.Where(g => g.Permissions.Any(p => p.PermissionKey.StartsWith(prefix)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(g => g.Name)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<PermissionGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.PermissionGroups
            .Include(g => g.Permissions)
            .Include(g => g.Tenant)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    public Task<PermissionGroup?> GetByIdUnscopedAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.PermissionGroups
            .IgnoreQueryFilters()
            .Include(g => g.Permissions)
            .Include(g => g.Tenant)
            .FirstOrDefaultAsync(g => g.Id == id && !g.Deleted, cancellationToken);

    public Task<bool> NameExistsAsync(Guid tenantId, string name, Guid excludeId, CancellationToken cancellationToken = default)
        => _dbContext.PermissionGroups
            .IgnoreQueryFilters()
            .AnyAsync(g => g.TenantId == tenantId && !g.Deleted && g.Id != excludeId && g.Name == name, cancellationToken);

    public async Task AddAsync(PermissionGroup group, CancellationToken cancellationToken = default)
        => await _dbContext.PermissionGroups.AddAsync(group, cancellationToken);

    public void Update(PermissionGroup group) => _dbContext.PermissionGroups.Update(group);

    public void Remove(PermissionGroup group) => _dbContext.PermissionGroups.Remove(group);

    // ---- Permission junction rows ----

    public void RemovePermissions(IEnumerable<PermissionGroupPermission> permissions)
        => _dbContext.PermissionGroupPermissions.RemoveRange(permissions);

    public async Task AddPermissionAsync(PermissionGroupPermission permission, CancellationToken cancellationToken = default)
        => await _dbContext.PermissionGroupPermissions.AddAsync(permission, cancellationToken);

    // ---- Role composition links ----

    public async Task<IReadOnlyList<PermissionGroup>> GetByRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        // Platform-level computation: ignore the tenant filter (a role spans tenants) but exclude soft-deleted groups.
        var groupIds = _dbContext.RolePermissionGroups.Where(l => l.RoleId == roleId).Select(l => l.PermissionGroupId);
        return await _dbContext.PermissionGroups
            .IgnoreQueryFilters()
            .Include(g => g.Permissions)
            .Where(g => groupIds.Contains(g.Id) && !g.Deleted)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RolePermissionGroup>> GetRoleLinksAsync(Guid roleId, CancellationToken cancellationToken = default)
        => await _dbContext.RolePermissionGroups.Where(l => l.RoleId == roleId).ToListAsync(cancellationToken);

    public Task<RolePermissionGroup?> GetRoleLinkAsync(Guid roleId, Guid groupId, CancellationToken cancellationToken = default)
        => _dbContext.RolePermissionGroups.FirstOrDefaultAsync(l => l.RoleId == roleId && l.PermissionGroupId == groupId, cancellationToken);

    public async Task<IReadOnlyList<(Guid RoleId, string RoleName)>> GetRolesUsingGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.RolePermissionGroups
            .Where(l => l.PermissionGroupId == groupId)
            .Join(_dbContext.Roles.IgnoreQueryFilters().Where(r => !r.Deleted), l => l.RoleId, r => r.Id, (l, r) => new { r.Id, r.Name })
            .ToListAsync(cancellationToken);
        return rows.Select(r => (r.Id, r.Name)).ToList();
    }

    public Task<int> CountRolesUsingGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
        => _dbContext.RolePermissionGroups.CountAsync(l => l.PermissionGroupId == groupId, cancellationToken);

    public async Task AddRoleLinkAsync(RolePermissionGroup link, CancellationToken cancellationToken = default)
        => await _dbContext.RolePermissionGroups.AddAsync(link, cancellationToken);

    public void RemoveRoleLink(RolePermissionGroup link) => _dbContext.RolePermissionGroups.Remove(link);

    // ---- Templates ----

    public async Task<IReadOnlyList<PermissionGroupTemplate>> GetTemplatesAsync(CancellationToken cancellationToken = default)
        => await _dbContext.PermissionGroupTemplates.OrderBy(t => t.Name).ToListAsync(cancellationToken);

    public Task<PermissionGroupTemplate?> GetTemplateByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.PermissionGroupTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task AddTemplateAsync(PermissionGroupTemplate template, CancellationToken cancellationToken = default)
        => await _dbContext.PermissionGroupTemplates.AddAsync(template, cancellationToken);

    public void UpdateTemplate(PermissionGroupTemplate template) => _dbContext.PermissionGroupTemplates.Update(template);

    public void RemoveTemplate(PermissionGroupTemplate template) => _dbContext.PermissionGroupTemplates.Remove(template);
}
