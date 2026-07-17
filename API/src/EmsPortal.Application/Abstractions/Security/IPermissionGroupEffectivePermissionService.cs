namespace EmsPortal.Application.Abstractions.Security;

/// <summary>One group's contribution to a role's effective permission set (preview source).</summary>
public sealed record EffectivePermissionSource(Guid GroupId, string GroupName, bool IsActive, IReadOnlyList<string> Keys);

/// <summary>The live, computed effective permissions for a role plus their per-group sources.</summary>
public sealed record EffectivePermissionPreview(IReadOnlyList<string> Permissions, IReadOnlyList<EffectivePermissionSource> Sources);

/// <summary>
/// Single owner of the denormalised <c>Role.EffectivePermissionsJson</c> cache (Permission Groups
/// ADR-002). Computes a role's effective permissions as the union of all permission keys from all
/// its <b>active</b> composed Permission Groups, and recomputes the cache on every group or
/// composition change. No other component writes that column.
/// </summary>
public interface IPermissionGroupEffectivePermissionService
{
    /// <summary>Recomputes and persists the cached effective permissions for a single role.</summary>
    Task RecomputeForRoleAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>Recomputes every role that currently includes the given group.</summary>
    Task RecomputeForGroupAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>Live (non-cached) union of a role's group permissions, with per-group sources, for previews.</summary>
    Task<EffectivePermissionPreview> PreviewForRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
}
