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

    /// <summary>
    /// The platform-standard option lists to seed. No platform-standard lists ship by default; a tenant
    /// creates its own lists via the Option Sets management UI.
    /// </summary>
    public static IReadOnlyList<Definition> All { get; } = Array.Empty<Definition>();
}
