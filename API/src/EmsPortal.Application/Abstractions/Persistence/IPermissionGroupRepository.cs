using EmsPortal.Domain.Entities;

namespace EmsPortal.Application.Abstractions.Persistence;

/// <summary>
/// Data access for Permission Groups, their permission-key junction rows, role-composition links,
/// and group templates. Tenant isolation is applied by the DbContext global query filter; admin /
/// cross-tenant reads pass an explicit tenant id and bypass it.
/// </summary>
public interface IPermissionGroupRepository
{
    // ---- Groups ----
    Task<(IReadOnlyList<PermissionGroup> Items, int Total)> ListAsync(
        Guid? tenantId, string? search, bool? isActive, bool? usedByRoles, string? category, int page, int limit, CancellationToken cancellationToken = default);

    /// <summary>Group with its permission keys loaded; tenant-scoped by the ambient filter.</summary>
    Task<PermissionGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Group with its permission keys loaded, ignoring the tenant filter (Super Admin cross-tenant).</summary>
    Task<PermissionGroup?> GetByIdUnscopedAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(Guid tenantId, string name, Guid excludeId, CancellationToken cancellationToken = default);

    Task AddAsync(PermissionGroup group, CancellationToken cancellationToken = default);

    void Update(PermissionGroup group);

    void Remove(PermissionGroup group);

    // ---- Permission junction rows ----
    void RemovePermissions(IEnumerable<PermissionGroupPermission> permissions);

    Task AddPermissionAsync(PermissionGroupPermission permission, CancellationToken cancellationToken = default);

    // ---- Role composition links ----
    /// <summary>The groups (active and inactive) composed into a role, each with its permission keys loaded.</summary>
    Task<IReadOnlyList<PermissionGroup>> GetByRoleAsync(Guid roleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RolePermissionGroup>> GetRoleLinksAsync(Guid roleId, CancellationToken cancellationToken = default);

    Task<RolePermissionGroup?> GetRoleLinkAsync(Guid roleId, Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>Roles (id + name) that currently include the group.</summary>
    Task<IReadOnlyList<(Guid RoleId, string RoleName)>> GetRolesUsingGroupAsync(Guid groupId, CancellationToken cancellationToken = default);

    Task<int> CountRolesUsingGroupAsync(Guid groupId, CancellationToken cancellationToken = default);

    Task AddRoleLinkAsync(RolePermissionGroup link, CancellationToken cancellationToken = default);

    void RemoveRoleLink(RolePermissionGroup link);

    // ---- Templates ----
    Task<IReadOnlyList<PermissionGroupTemplate>> GetTemplatesAsync(CancellationToken cancellationToken = default);

    Task<PermissionGroupTemplate?> GetTemplateByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddTemplateAsync(PermissionGroupTemplate template, CancellationToken cancellationToken = default);

    void UpdateTemplate(PermissionGroupTemplate template);

    void RemoveTemplate(PermissionGroupTemplate template);
}
