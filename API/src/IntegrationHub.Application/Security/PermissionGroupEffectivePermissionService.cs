using System.Text.Json;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Security;
using IntegrationHub.Domain.Entities;

namespace IntegrationHub.Application.Security;

/// <summary>
/// Default <see cref="IPermissionGroupEffectivePermissionService"/>. The single owner of the
/// denormalised <c>Role.EffectivePermissionsJson</c> cache: a role's group-derived permission set
/// is the union of the keys of all its <b>active</b> composed Permission Groups (ADR-002).
/// </summary>
public sealed class PermissionGroupEffectivePermissionService : IPermissionGroupEffectivePermissionService
{
    private readonly IRoleRepository _roles;
    private readonly IPermissionGroupRepository _groups;
    private readonly IUnitOfWork _unitOfWork;

    public PermissionGroupEffectivePermissionService(IRoleRepository roles, IPermissionGroupRepository groups, IUnitOfWork unitOfWork)
    {
        _roles = roles;
        _groups = groups;
        _unitOfWork = unitOfWork;
    }

    public async Task RecomputeForRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var role = await _roles.GetByIdAsync(roleId, cancellationToken);
        if (role is null)
        {
            return;
        }

        await ApplyAsync(role, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RecomputeForGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var affected = await _groups.GetRolesUsingGroupAsync(groupId, cancellationToken);
        foreach (var (roleId, _) in affected)
        {
            var role = await _roles.GetByIdAsync(roleId, cancellationToken);
            if (role is not null)
            {
                await ApplyAsync(role, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<EffectivePermissionPreview> PreviewForRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var groups = await _groups.GetByRoleAsync(roleId, cancellationToken);
        var sources = groups
            .Select(g => new EffectivePermissionSource(
                g.Id, g.Name, g.IsActive,
                g.Permissions.Select(p => p.PermissionKey).Distinct().OrderBy(k => k).ToList()))
            .ToList();
        var permissions = Union(groups);
        return new EffectivePermissionPreview(permissions, sources);
    }

    /// <summary>Recomputes and stages (no save) the role's cached group-derived permissions.</summary>
    private async Task ApplyAsync(Role role, CancellationToken cancellationToken)
    {
        var groups = await _groups.GetByRoleAsync(role.Id, cancellationToken);
        role.EffectivePermissionsJson = JsonSerializer.Serialize(Union(groups));
        _roles.Update(role);
    }

    /// <summary>Union of the permission keys of all <b>active</b> groups (deactivated groups contribute zero).</summary>
    private static List<string> Union(IEnumerable<PermissionGroup> groups)
        => groups
            .Where(g => g.IsActive)
            .SelectMany(g => g.Permissions.Select(p => p.PermissionKey))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();
}
