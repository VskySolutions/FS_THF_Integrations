namespace EmsPortal.Domain.Enums;

/// <summary>
/// How a <see cref="Entities.OptionSet"/>'s items are ordered when presented in a picker.
/// Alphabetical modes are computed from the item label and ignore the stored sort order;
/// <see cref="Custom"/> honours each item's <see cref="Entities.OptionSetItem.SortOrder"/>,
/// which an admin sets by dragging items up and down.
/// </summary>
public enum OptionItemSortMode
{
    /// <summary>Order items by label, A→Z. The stored sort order is ignored.</summary>
    AlphabeticalAsc = 0,

    /// <summary>Order items by label, Z→A. The stored sort order is ignored.</summary>
    AlphabeticalDesc = 1,

    /// <summary>Honour each item's explicit <c>SortOrder</c>; the admin reorders by dragging.</summary>
    Custom = 2,
}
