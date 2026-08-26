using System.Text.Json;

namespace EmsPortal.Domain.Entities;

/// <summary>
/// A named set of permissions (RBAC), assigned to users. Two kinds live here, told apart by
/// <see cref="TenantId"/>: PLATFORM roles, which a Super Admin maintains for the whole platform (every
/// seeded system role is one), and TENANT roles, which a tenant admin creates for their own firm and
/// which never leave it. System roles (SuperAdmin/TenantAdmin) are seeded and cannot be deleted.
/// </summary>
public class Role : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>
    /// The tenant that owns this role, or <c>null</c> for a platform role. A platform role is offered in
    /// every tenant and only a Super Admin may change it; a tenant-owned role was created inside that one
    /// tenant and is invisible everywhere else — it never appears in another tenant's list or user picker,
    /// and only its own tenant's admins may edit, delete, or assign it.
    /// </summary>
    public Guid? TenantId { get; set; }

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
