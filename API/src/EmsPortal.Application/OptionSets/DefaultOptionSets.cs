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
    /// <summary>
    /// <paramref name="Description"/> is what the value MEANS, surfaced as its tooltip wherever the value
    /// is offered or displayed. Worth filling in wherever the labels alone could be mistaken for one
    /// another; left null where the label speaks for itself.
    /// </summary>
    public sealed record ItemDefinition(
        string Value,
        string Label,
        int SortOrder,
        string? MetadataJson = null,
        string? Description = null);

    public sealed record Definition(
        EntityType EntityType,
        string Key,
        string Name,
        OptionItemSortMode ItemSortMode,
        IReadOnlyList<ItemDefinition> Items);

    /// <summary>
    /// The platform-standard option lists to seed. Most of the REMS lists keyed to
    /// <see cref="EntityType.Rems"/> are CODE-valued — the chosen item's <c>Value</c> is what gets stored
    /// on the REMS / REMSForm / REMSEngagement row. Two are not: the grouped marketing-methods list and the
    /// tax-form checklist are referenced by item ID, as foreign keys from
    /// <c>REMSEngagementMarketingMethod</c> and <c>REMSEngagementTaxForm</c>. Each marketing item carries
    /// its group and behaviour flags in <see cref="ItemDefinition.MetadataJson"/>.
    /// </summary>
    public static IReadOnlyList<Definition> All { get; } = new[]
    {
        new Definition(EntityType.Rems, "REMS.Type", "REMS Type", OptionItemSortMode.Custom, new[]
        {
            // Three ways a referral can relate to THF's records. "New Engagement" and "Existing Client"
            // used to be separate values, which asked the partner to split a hair nobody could: every
            // new engagement for a client we already have is both. They are one value now — the code
            // `existing_client` survived the merge (see MergeRemsExistingClientTypes), so the rows and
            // the REMS_EXISTING_CLIENT_TYPES marking rule that read it need no translation.
            new ItemDefinition("brand_new_client", "Brand-New Client", 1, Description:
                "The client/company is working with THF for the first time. No prior record exists in the system."),
            new ItemDefinition("existing_client", "New Engagement, Existing Client", 2, Description:
                "The person or company already has an active client record with THF, and this request " +
                "creates an additional engagement under that same client."),
            new ItemDefinition("subsidiary_child_of_existing_client", "Subsidiary / Child of Existing Client", 3, Description:
                "When the client is a child of an already present parent client. All the billing goes to " +
                "the parent client in this situation."),
        }),
        // How the client heard about THF, asked on the public EMS form. The descriptions double as the
        // examples the client needs to place themselves, so they carry the "Friend, Family, or Colleague"
        // detail rather than crowding it into the label.
        new Definition(EntityType.Rems, "REMS.ReferralSource", "REMS Referral Source", OptionItemSortMode.Custom, new[]
        {
            new ItemDefinition("referral", "Referral", 1, Description: "Friend, Family, or Colleague."),
            new ItemDefinition("search_engine", "Search Engine", 2, Description: "Google, Bing, Yahoo."),
            new ItemDefinition("digital_ad_social", "Digital Ad / Social Media", 3, Description:
                "Facebook, Instagram, LinkedIn."),
            new ItemDefinition("event_conference", "Event or Conference", 4, Description:
                "Trade shows, webinars, or local community events."),
            new ItemDefinition("print_broadcast", "Print or Broadcast", 5, Description:
                "Direct mailers, billboards, TV, or radio ads."),
            new ItemDefinition("website_blog", "Website or Blog", 6, Description:
                "Mentioned in an article, forum (e.g., Reddit), or guest post."),
            new ItemDefinition("other", "Other", 7, Description: "Anything not covered above."),
        }),
        // REMS.Priority is gone: the field it configured was dropped from the request (see
        // DropRemsRequestPriority). A list nobody reads is worse than no list — it invites a tenant to
        // configure something that has no effect anywhere.
        new Definition(EntityType.Rems, "REMS.Status", "REMS Status", OptionItemSortMode.Custom, new[]
        {
            // The request lifecycle, in stage order — each value names who the request is waiting on.
            // See RemsRequestStatuses for the transitions.
            //
            // "submitted" is gone with the Admin Pool it described: the initiator now sends the intake
            // form to the client themselves, so a request never waits in a pool to be picked up.
            // "customer_submitted" keeps its code and is relabelled — the stage is the Admin reviewing
            // what came back, not staff starting the engagement setup, which happens before any of this.
            new ItemDefinition("draft", "Draft", 1, Description:
                "With its initiator. Saved but not yet sent to the client."),
            new ItemDefinition("awaiting_customer", "Awaiting Customer", 2, Description:
                "The intake form has been emailed. The ball is with the client."),
            new ItemDefinition("customer_submitted", "Admin Review", 3, Description:
                "The client's answers are in and the named Admin is reviewing them."),
            new ItemDefinition("returned_to_initiator", "Returned to Initiator", 4, Description:
                "The Admin sent the engagement setup back for rework, with a reason. Client intake is read-only."),
            new ItemDefinition("awaiting_admin_confirmation", "Awaiting Admin Confirmation", 5, Description:
                "The initiator revised the setup and handed it back for the Admin to confirm."),
            new ItemDefinition("pending_approval", "Pending Approval", 6, Description:
                "Routed to the approvers. Every field is read-only while a round is open."),
            new ItemDefinition("changes_requested", "Changes Requested", 7, Description:
                "Enough approvers declined to close the round. Back with the initiator to rework the setup."),
            new ItemDefinition("approved", "Approved", 8, Description:
                "Fully approved. Permanently read-only."),
        }),
        new Definition(EntityType.Rems, "REMS.BillingPeriod", "REMS Billing Period", OptionItemSortMode.Custom, new[]
        {
            // How often the client is billed. Pairs with the engagement's No. of Bills, which is a plain
            // count rather than anything derived from the period.
            new ItemDefinition("monthly", "Monthly", 1),
            new ItemDefinition("quarterly", "Quarterly", 2),
            new ItemDefinition("annual", "Annual", 3),
        }),
        new Definition(EntityType.Rems, "REMS.IndustryGroup", "REMS Industry Group", OptionItemSortMode.Custom, new[]
        {
            // "Business" was split into the three kinds THF actually onboards. All three ask the client
            // exactly the same questions the old single group did (EIN, CEO/CFO/AP, banker, lawyer) — see
            // RemsFormPayloadValidator.IsBusinessGroup, which is what keeps them one family on the form.
            new ItemDefinition("individual", "Individual", 1),
            new ItemDefinition("not_for_profit", "Not-for-Profit", 2),
            new ItemDefinition("insurance", "Insurance", 3),
            new ItemDefinition("commercial", "Commercial", 4),
            new ItemDefinition("government", "Government", 5),
        }),
        // The client's trade, one level below the industry group. Unlike the group above — which decides
        // which questions the client's intake form asks and is therefore frozen once that form goes out —
        // this is an internal classification only, so it stays editable for as long as the setup does.
        // The list spans every group's trades in one flat list rather than being filtered by the chosen
        // group: the groups do not partition cleanly (a hospital is Health Care under Commercial and under
        // Not-for-Profit alike), and a tenant adding a trade should not have to say which groups may see it.
        new Definition(EntityType.Rems, "REMS.SubIndustry", "REMS Sub-Industry", OptionItemSortMode.Custom, new[]
        {
            new ItemDefinition("affordable_housing", "Affordable Housing", 1),
            new ItemDefinition("agribusiness", "Agribusiness", 2),
            new ItemDefinition("auto_dealers", "Auto Dealers", 3),
            new ItemDefinition("construction", "Construction", 4),
            new ItemDefinition("entertainment", "Entertainment", 5),
            new ItemDefinition("financial_institutions_banking", "Financial Institutions/Banking", 6),
            new ItemDefinition("hospitality", "Hospitality", 7),
            new ItemDefinition("manufacturing", "Manufacturing", 8),
            new ItemDefinition("professional_service_firms", "Professional Service Firms", 9),
            new ItemDefinition("real_estate", "Real Estate", 10),
            new ItemDefinition("retail", "Retail", 11),
            new ItemDefinition("health_care", "Health Care", 12),
            new ItemDefinition("oil_gas_distribution", "Oil & Gas Distribution", 13),
            new ItemDefinition("wholesale", "Wholesale", 14),
            new ItemDefinition("technology", "Technology", 15),
            new ItemDefinition("state_government", "State Government", 16),
            new ItemDefinition("local_government", "Local Government", 17),
            new ItemDefinition("federal_government", "Federal Government", 18),
            new ItemDefinition("educational_institutions", "Educational Institutions", 19),
            new ItemDefinition("insurance_property_casualty", "Insurance - Property and Casualty", 20),
            new ItemDefinition("insurance_life", "Insurance - Life", 21),
            new ItemDefinition("insurance_other", "Insurance - Other", 22),
            new ItemDefinition("trade_associations", "Trade Associations", 23),
            new ItemDefinition("charitable_organizations_foundations", "Charitable Organizations or Foundations", 24),
            new ItemDefinition("other_not_for_profit", "Other Not-for-Profit", 25),
            // Kept alongside the three tiers above it: not every government client is filed as state,
            // local or federal, and the unqualified value is what those are recorded under.
            new ItemDefinition("government", "Government", 26),
            new ItemDefinition("individual", "Individual", 27),
            new ItemDefinition("distribution", "Distribution", 28),
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
        // The actual service being sold, one level below the service line. Nothing branches on it — the
        // Government Audit rule keys off the LINE — so it is a classification field: what the firm is being
        // engaged to do, for reporting and for the billing/marketing view of the engagement. The Internal-*
        // values are the firm's own work, booked as engagements so the same setup and approval route covers
        // them.
        new Definition(EntityType.Rems, "REMS.SubServiceLine", "REMS Sub-Service Line", OptionItemSortMode.Custom, new[]
        {
            new ItemDefinition("attest_services", "Attest Services", 1),
            new ItemDefinition("tax_compliance", "Tax Compliance", 2),
            new ItemDefinition("client_accounting_services", "Client Accounting Services", 3),
            new ItemDefinition("outsourced_cfo", "Outsourced CFO", 4),
            new ItemDefinition("consulting", "Consulting", 5),
            new ItemDefinition("business_valuation", "Business Valuation", 6),
            new ItemDefinition("it_services", "IT Services", 7),
            new ItemDefinition("plan_administration", "Plan Administration", 8),
            new ItemDefinition("mergers_acquisitions", "Mergers & Acquisitions", 9),
            new ItemDefinition("payroll_services", "Payroll Services", 10),
            new ItemDefinition("peer_review", "Peer Review", 11),
            new ItemDefinition("soc", "SOC", 12, Description:
                "System and Organization Controls reporting (SOC 1 / SOC 2)."),
            new ItemDefinition("employee_benefits", "Employee Benefits", 13),
            new ItemDefinition("estate_planning", "Estate Planning", 14),
            new ItemDefinition("litigation_support", "Litigation Support", 15),
            new ItemDefinition("forensic_accounting", "Forensic Accounting", 16),
            new ItemDefinition("internal_accounting", "Internal-Accounting", 17),
            new ItemDefinition("internal_billing", "Internal-Billing", 18),
            new ItemDefinition("internal_operations", "Internal-Operations", 19),
            new ItemDefinition("internal_marketing", "Internal-Marketing", 20),
            new ItemDefinition("internal_it", "Internal-IT", 21),
            new ItemDefinition("internal_miscellaneous", "Internal-Miscellaneous", 22),
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
