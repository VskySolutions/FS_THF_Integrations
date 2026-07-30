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
    /// The platform-standard option lists to seed. The REMS feature (WO-110) ships five standard lists
    /// keyed to <see cref="EntityType.Rems"/>: request type, priority, status and industry group (whose
    /// codes are stored on the REMS/REMSForm rows), plus the grouped marketing-methods list whose items
    /// are referenced by foreign key from <c>REMSEngagementMarketingMethod</c>. Each marketing item
    /// carries its group and behaviour flags in <see cref="ItemDefinition.MetadataJson"/>.
    /// </summary>
    public static IReadOnlyList<Definition> All { get; } = new[]
    {
        new Definition(EntityType.Rems, "REMS.Type", "REMS Type", OptionItemSortMode.Custom, new[]
        {
            new ItemDefinition("brand_new_client", "Brand-New Client", 1),
            new ItemDefinition("new_engagement", "New Engagement", 2),
            new ItemDefinition("existing_client", "Existing Client", 3),
            new ItemDefinition("subsidiary_child_of_existing_client", "Subsidiary / Child of Existing Client", 4),
        }),
        new Definition(EntityType.Rems, "REMS.Priority", "REMS Priority", OptionItemSortMode.Custom, new[]
        {
            new ItemDefinition("urgent", "Urgent", 1),
            new ItemDefinition("high", "High", 2),
            new ItemDefinition("medium", "Medium", 3),
            new ItemDefinition("low", "Low", 4),
        }),
        new Definition(EntityType.Rems, "REMS.Status", "REMS Status", OptionItemSortMode.Custom, new[]
        {
            new ItemDefinition("draft", "Draft", 1),
            new ItemDefinition("submitted", "Submitted", 2),
            new ItemDefinition("sent", "Sent", 3),
            new ItemDefinition("awaiting_customer", "Awaiting Customer", 4),
            new ItemDefinition("customer_submitted", "Customer Submitted", 5),
            new ItemDefinition("approved", "Approved", 6),
            new ItemDefinition("rejected", "Rejected", 7),
        }),
        new Definition(EntityType.Rems, "REMS.IndustryGroup", "REMS Industry Group", OptionItemSortMode.Custom, new[]
        {
            new ItemDefinition("individual", "Individual", 1),
            new ItemDefinition("business", "Business", 2),
            new ItemDefinition("government", "Government", 3),
        }),
        new Definition(EntityType.Rems, "REMSMarketing_MarketingMethods.MarketingMethodId", "REMS Marketing Methods", OptionItemSortMode.Custom, new[]
        {
            // Global — not auto-suggested, not editable.
            new ItemDefinition("all", "All", 1, MarketingMetadata("Global", autoSuggested: false, editable: false)),
            new ItemDefinition("active_clients", "Active Clients", 2, MarketingMetadata("Global", autoSuggested: false, editable: false)),
            // Geography — auto-suggested and editable.
            new ItemDefinition("tallahassee", "Tallahassee", 3, MarketingMetadata("Geography", autoSuggested: true, editable: true)),
            new ItemDefinition("panama_city_bay_county", "Panama City-Bay County", 4, MarketingMetadata("Geography", autoSuggested: true, editable: true)),
            new ItemDefinition("lakeland_dade_city", "Lakeland-Dade City", 5, MarketingMetadata("Geography", autoSuggested: true, editable: true)),
            new ItemDefinition("tampa", "TAMPA", 6, MarketingMetadata("Geography", autoSuggested: true, editable: true)),
            // Service / Education — auto-suggested and editable.
            new ItemDefinition("tax", "Tax", 7, MarketingMetadata("Service/Education", autoSuggested: true, editable: true)),
            new ItemDefinition("audit_nfp", "Audit-NFP", 8, MarketingMetadata("Service/Education", autoSuggested: true, editable: true)),
            new ItemDefinition("audit_insurance", "Audit-Insurance", 9, MarketingMetadata("Service/Education", autoSuggested: true, editable: true)),
            new ItemDefinition("gcs", "GCS", 10, MarketingMetadata("Service/Education", autoSuggested: true, editable: true)),
            new ItemDefinition("cas", "CAS", 11, MarketingMetadata("Service/Education", autoSuggested: true, editable: true)),
            // Event — not auto-suggested, not editable.
            new ItemDefinition("tax_event", "Tax Event", 12, MarketingMetadata("Event", autoSuggested: false, editable: false)),
            new ItemDefinition("finrep_conference", "FINREP Conference", 13, MarketingMetadata("Event", autoSuggested: false, editable: false)),
        }),
    };

    /// <summary>Builds the <c>MetadataJson</c> for a REMS marketing-method item: its group and behaviour flags.</summary>
    private static string MarketingMetadata(string group, bool autoSuggested, bool editable)
        => $"{{\"group\":\"{group}\",\"autoSuggested\":{(autoSuggested ? "true" : "false")},\"editable\":{(editable ? "true" : "false")}}}";
}
