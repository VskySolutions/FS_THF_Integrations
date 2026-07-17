using EmsPortal.Domain.Enums;

namespace EmsPortal.Application.OptionSets;

/// <summary>
/// Platform-standard option lists seeded on startup (TenantId = null, IsSystem = true). They are
/// visible to every tenant and read-only in the app; a tenant adds its own lists/values rather than
/// editing these. Mirrors the <c>DefaultEmailTemplates</c> definition pattern.
/// </summary>
public static class DefaultOptionSets
{
    public sealed record ItemDefinition(string Value, string Label, int SortOrder, string? MetadataJson = null);

    public sealed record Definition(
        EntityType EntityType,
        string Key,
        string Name,
        OptionItemSortMode ItemSortMode,
        IReadOnlyList<ItemDefinition> Items);

    /// <summary>Standard Payment Terms for Customer records (kept in numeric order via Custom sort).</summary>
    public static readonly Definition PaymentTerms = new(
        EntityType.CustomerRequest,
        "payment_terms",
        "Payment Terms",
        OptionItemSortMode.Custom,
        new[]
        {
            new ItemDefinition("net_30", "NET 30", 0, "{\"days\":30}"),
            new ItemDefinition("net_60", "NET 60", 1, "{\"days\":60}"),
            new ItemDefinition("net_90", "NET 90", 2, "{\"days\":90}"),
            new ItemDefinition("net_120", "NET 120", 3, "{\"days\":120}"),
        });

    public static IReadOnlyList<Definition> All { get; } = new[] { PaymentTerms };
}
