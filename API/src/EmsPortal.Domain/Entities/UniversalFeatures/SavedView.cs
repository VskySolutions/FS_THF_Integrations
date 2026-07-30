namespace EmsPortal.Domain.Entities;

/// <summary>
/// A saved list-page configuration (filters, sort, columns). Private to a user when
/// <see cref="UserId"/> is set, or shared across the tenant when <see cref="IsShared"/> is true.
/// </summary>
public class SavedView : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The owning user; null for a purely tenant-shared view with no personal owner.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Display name of the view.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The list page key the view applies to (e.g. <c>users</c>).</summary>
    public string ListPage { get; set; } = string.Empty;

    /// <summary>Serialised filter state (JSON).</summary>
    public string? FiltersJson { get; set; }

    /// <summary>Serialised sort state (JSON).</summary>
    public string? SortJson { get; set; }

    /// <summary>Serialised visible-columns state (JSON).</summary>
    public string? ColumnsJson { get; set; }

    /// <summary>True when the view is shared with the whole tenant.</summary>
    public bool IsShared { get; set; }
}
