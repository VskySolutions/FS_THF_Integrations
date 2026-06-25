using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Domain.Entities;

/// <summary>
/// A named list of selectable input values scoped to an entity type and a tenant — e.g. the
/// "Payment Terms" choices (NET 30, NET 60, …) shown on a Customer record. A row with a null
/// <see cref="TenantId"/> is a platform-standard list seeded in application code (visible to every
/// tenant, see <see cref="IsSystem"/>); a row with a <see cref="TenantId"/> is that tenant's own list.
/// <para>
/// Lists are either standalone (single group) or part of a dependency chain: a set whose
/// <see cref="ParentSetId"/> is non-null is driven by a parent set, and its items point at a parent
/// item via <see cref="OptionSetItem.ParentItemId"/> (e.g. State depends on Country). Inherits the
/// standard audit/soft-delete fields.
/// </para>
/// </summary>
public class OptionSet : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant, or null for a platform-standard (seeded) list.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>The entity type this list applies to (e.g. CustomerRequest).</summary>
    public EntityType EntityType { get; set; }

    /// <summary>Stable programmatic code, unique per (tenant, entity), e.g. <c>payment_terms</c>.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Human-friendly display name, e.g. "Payment Terms".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// For a dependency list, the parent set that drives it; null for a standalone (single) list.
    /// </summary>
    public Guid? ParentSetId { get; set; }

    /// <summary>How the items are ordered in a picker.</summary>
    public OptionItemSortMode ItemSortMode { get; set; } = OptionItemSortMode.Custom;

    /// <summary>True for a seeded platform-standard list; such lists are read-only in the app.</summary>
    public bool IsSystem { get; set; }

    /// <summary>When false, the whole list is hidden from pickers without being deleted.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>The values belonging to this list.</summary>
    public ICollection<OptionSetItem> Items { get; set; } = new List<OptionSetItem>();
}
