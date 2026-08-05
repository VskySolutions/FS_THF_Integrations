using EmsPortal.Domain.Enums;

namespace EmsPortal.Application.OptionSets;

/// <summary>
/// Platform-standard option lists seeded on startup (TenantId = null, IsSystem = true), visible to every
/// tenant. They are the STARTING values, not fixed ones — their items can be managed in the app, and a
/// tenant created via <see cref="TenantOptionSetSeeder"/> gets its own copy. Because the seeded rows are
/// shared, editing one there changes it for every tenant that has no copy of its own.
/// Mirrors the <c>DefaultEmailTemplates</c> definition pattern.
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
            // The request lifecycle, in stage order — each value names who the request is waiting on.
            // See RemsRequestStatuses for the transitions and the per-engagement roll-up.
            new ItemDefinition("draft", "Draft", 1),
            new ItemDefinition("submitted", "Submitted", 2),
            new ItemDefinition("awaiting_customer", "Awaiting Customer", 3),
            new ItemDefinition("customer_submitted", "Engagement Setup", 4),
            new ItemDefinition("pending_approval", "Pending Approval", 5),
            new ItemDefinition("changes_requested", "Changes Requested", 6),
            new ItemDefinition("approved", "Approved", 7),
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
        // Engagement department (WO-114). The code drives the conditional engagement detail: an "audit"
        // engagement carries an audit detail (signed CAF), a "tax" engagement a tax detail (fiscal year
        // end + calculated due dates + form checklist). Also the key for the department-to-director map.
        new Definition(EntityType.Rems, "REMS.Department", "REMS Department", OptionItemSortMode.Custom, new[]
        {
            new ItemDefinition("cas", "CAS", 1),
            new ItemDefinition("tax", "Tax", 2),
            new ItemDefinition("audit", "Audit", 3),
            new ItemDefinition("gcs", "GCS", 4),
        }),
        // Engagement service line (WO-114). An "audit" department with the "government" service line is a
        // Government Audit and additionally requires a contract number + Florida 1% state-fee flag.
        new Definition(EntityType.Rems, "REMS.ServiceLine", "REMS Service Line", OptionItemSortMode.Custom, new[]
        {
            new ItemDefinition("commercial", "Commercial", 1),
            new ItemDefinition("non_profit", "Non-Profit", 2),
            new ItemDefinition("government", "Government", 3),
            new ItemDefinition("individual", "Individual", 4),
        }),
        // Engagement tax forms (WO-114): the checklist values referenced by foreign key from
        // REMSEngagementTaxForm.TaxFormId on a tax engagement's tax detail.
        new Definition(EntityType.Rems, "REMS.TaxForm", "REMS Tax Form", OptionItemSortMode.Custom, new[]
        {
            new ItemDefinition("1040", "1040 — Individual", 1),
            new ItemDefinition("1120", "1120 — C Corporation", 2),
            new ItemDefinition("1120_s", "1120-S — S Corporation", 3),
            new ItemDefinition("1065", "1065 — Partnership", 4),
            new ItemDefinition("990", "990 — Tax-Exempt", 5),
        }),
        // A user's job title, ordered by seniority then function. Unlike the REMS lists above, the LABEL is
        // what gets stored (on Person.JobTitle, an existing free-text field already shown verbatim in the
        // People list and the profile), so a title reads correctly everywhere without a lookup — and the
        // free-text values already in the database stay valid.
        new Definition(EntityType.User, "User.JobTitle", "Job Title", OptionItemSortMode.Custom, new[]
        {
            new ItemDefinition("managing_shareholder", "Managing Shareholder", 1),
            new ItemDefinition("shareholder", "Shareholder", 2),
            new ItemDefinition("partner", "Partner", 3),
            new ItemDefinition("principal", "Principal", 4),
            new ItemDefinition("director", "Director", 5),
            new ItemDefinition("senior_manager", "Senior Manager", 6),
            new ItemDefinition("manager", "Manager", 7),
            new ItemDefinition("supervisor", "Supervisor", 8),
            new ItemDefinition("audit_manager", "Audit Manager", 9),
            new ItemDefinition("audit_senior", "Audit Senior", 10),
            new ItemDefinition("tax_manager", "Tax Manager", 11),
            new ItemDefinition("tax_senior", "Tax Senior", 12),
            new ItemDefinition("senior_accountant", "Senior Accountant", 13),
            new ItemDefinition("staff_accountant", "Staff Accountant", 14),
            new ItemDefinition("senior_associate", "Senior Associate", 15),
            new ItemDefinition("associate", "Associate", 16),
            new ItemDefinition("paraprofessional", "Paraprofessional", 17),
            new ItemDefinition("bookkeeper", "Bookkeeper", 18),
            new ItemDefinition("payroll_specialist", "Payroll Specialist", 19),
            new ItemDefinition("controller", "Controller", 20),
            new ItemDefinition("client_service_executive", "Client Service Executive", 21),
            new ItemDefinition("billing_manager", "Billing Manager", 22),
            new ItemDefinition("business_development_manager", "Business Development Manager", 23),
            new ItemDefinition("marketing_coordinator", "Marketing Coordinator", 24),
            new ItemDefinition("human_resources_manager", "Human Resources Manager", 25),
            new ItemDefinition("office_manager", "Office Manager", 26),
            new ItemDefinition("it_administrator", "IT Administrator", 27),
            new ItemDefinition("administrative_assistant", "Administrative Assistant", 28),
            new ItemDefinition("intern", "Intern", 29),
        }),
    };

    /// <summary>Builds the <c>MetadataJson</c> for a REMS marketing-method item: its group and behaviour flags.</summary>
    private static string MarketingMetadata(string group, bool autoSuggested, bool editable)
        => $"{{\"group\":\"{group}\",\"autoSuggested\":{(autoSuggested ? "true" : "false")},\"editable\":{(editable ? "true" : "false")}}}";
}
