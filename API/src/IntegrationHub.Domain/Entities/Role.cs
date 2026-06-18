using System.Text.Json;

namespace IntegrationHub.Domain.Entities;

/// <summary>
/// A named set of permissions (RBAC). Created and managed by Super Admins, made
/// available to tenants via <see cref="TenantRole"/>, and assigned to users.
/// System roles (SuperAdmin/TenantAdmin/Operator) are seeded and cannot be deleted.
/// </summary>
public class Role : AuditableEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Seeded, non-deletable role mirroring a base authorization level.</summary>
    public bool IsSystem { get; set; }

    /// <summary>Direct permission keys (see Shared.Security.Permissions). Mapped as a JSON column.</summary>
    public List<string> Permissions { get; set; } = new();

    /// <summary>
    /// Denormalised cache of the permission keys contributed by the role's composed
    /// <see cref="PermissionGroup"/>s (Permission Groups feature, ADR-002). Maintained solely by the
    /// effective-permission service on every group/composition change; never edited directly.
    /// </summary>
    public string? EffectivePermissionsJson { get; set; }

    /// <summary>Groups composed into this role.</summary>
    public ICollection<RolePermissionGroup> GroupLinks { get; set; } = new List<RolePermissionGroup>();

    /// <summary>Parsed view of <see cref="EffectivePermissionsJson"/> (the group-derived permission keys).</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public IReadOnlyList<string> EffectivePermissions
    {
        get
        {
            if (string.IsNullOrWhiteSpace(EffectivePermissionsJson))
            {
                return Array.Empty<string>();
            }
            try
            {
                return JsonSerializer.Deserialize<List<string>>(EffectivePermissionsJson) ?? new List<string>();
            }
            catch (JsonException)
            {
                return Array.Empty<string>();
            }
        }
    }
}
