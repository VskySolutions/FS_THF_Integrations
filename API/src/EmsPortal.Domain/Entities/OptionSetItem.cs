namespace EmsPortal.Domain.Entities;

/// <summary>
/// A single selectable value within an <see cref="OptionSet"/> — e.g. "NET 30" in the Payment Terms
/// list. A null <see cref="TenantId"/> is a standard (seeded) value; a tenant may add its own values
/// (with its <see cref="TenantId"/>) to a standard list. For a dependency list, <see cref="ParentItemId"/>
/// links the value to the parent item it is valid under (e.g. State "California" → Country "USA").
/// Inherits the standard audit/soft-delete fields.
/// </summary>
public class OptionSetItem : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>The list this value belongs to.</summary>
    public Guid OptionSetId { get; set; }

    /// <summary>Owning tenant, or null for a standard (seeded) value.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// For a dependency list, the parent-set item this value is valid under; null for a single group.
    /// </summary>
    public Guid? ParentItemId { get; set; }

    /// <summary>Stable programmatic code, unique per (set, tenant), e.g. <c>net_30</c>.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Human-friendly display label, e.g. "NET 30".</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Explicit position, honoured only when the set's sort mode is Custom.</summary>
    public int SortOrder { get; set; }

    /// <summary>When true, this value is pre-selected by default in the picker.</summary>
    public bool IsDefault { get; set; }

    /// <summary>When false, the value is hidden from pickers without being deleted.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Optional background colour (hex, e.g. "#e3f2fd") shown for this value in the UI.</summary>
    public string? BackgroundColor { get; set; }

    /// <summary>Optional text colour (hex, e.g. "#0d47a1") shown for this value in the UI.</summary>
    public string? TextColor { get; set; }

    /// <summary>Optional extra metadata as a JSON object, e.g. <c>{"days":30}</c>.</summary>
    public string? MetadataJson { get; set; }

    /// <summary>The owning list (navigation).</summary>
    public OptionSet? OptionSet { get; set; }
}
