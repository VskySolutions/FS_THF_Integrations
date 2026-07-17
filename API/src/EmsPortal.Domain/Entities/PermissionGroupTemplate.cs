using System.Text.Json;

namespace EmsPortal.Domain.Entities;

/// <summary>
/// A pre-defined, reusable group configuration (name + permission set) administrators can use as a
/// starting point when creating a <see cref="PermissionGroup"/>. Platform-level (available to all
/// tenants). Seeded templates (<see cref="IsSeeded"/>) cannot be deleted by Tenant Admins.
/// </summary>
public class PermissionGroupTemplate : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Template name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional description (pre-populated into groups created from this template).</summary>
    public string? Description { get; set; }

    /// <summary>True for platform-seeded templates (non-deletable by Tenant Admins).</summary>
    public bool IsSeeded { get; set; }

    /// <summary>The template's permission keys, stored as a JSON array string.</summary>
    public string PermissionKeysJson { get; set; } = "[]";

    /// <summary>Parsed view of <see cref="PermissionKeysJson"/>.</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public IReadOnlyList<string> PermissionKeys
    {
        get
        {
            if (string.IsNullOrWhiteSpace(PermissionKeysJson))
            {
                return Array.Empty<string>();
            }
            try
            {
                return JsonSerializer.Deserialize<List<string>>(PermissionKeysJson) ?? new List<string>();
            }
            catch (JsonException)
            {
                return Array.Empty<string>();
            }
        }
    }
}
