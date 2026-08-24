using System.Text.Json;

namespace EmsPortal.Api.Models.Rems;

// ---------------------------------------------------------------------------------------------------
// WO-114 — REMS engagement workspace (Part A) + copy/marketing/commission (Part B). The submitted-form
// view (rendered from the immutable submission payload) is intentionally SEPARATE from the editable
// workspace graph (client + entities + engagements + details).
// ---------------------------------------------------------------------------------------------------

/// <summary>
/// One row of the client-forms list (AC-REMS-013.1): a request that has (or once had) an EMS form, with
/// its submitted/not-submitted state, client name, submission date, and assigned Admin/CSE.
/// </summary>
public sealed record RemsClientFormRow(
    Guid RemsId,
    string RemsNumber,
    string ClientName,
    string RequestStatus,
    bool HasForm,
    bool Submitted,
    DateTime? SubmittedOnUtc,
    /// <summary>The admin holding this request, or null while it is still waiting for one to pick it up.</summary>
    RemsUserRef? AssignedAdmin,
    RemsUserRef? Cse,
    /// <summary>
    /// This caller may claim the request. True only on an unclaimed one, and only for a caller holding
    /// <c>rems.requests.assign</c> — the same pair <c>RemsRequestsController.PickUp</c> enforces, asked
    /// ahead of the click so the list can offer the button rather than let it 409.
    /// </summary>
    bool CanPickUp,
    /// <summary>
    /// This caller may put the request back in the pool — the undo of Pick up, for the admin who claimed
    /// something by mistake. True on a request this caller HOLDS, and on any claimed request for an
    /// elevated caller, which is the same test <c>RemsRequestsController.HandBack</c> applies.
    /// </summary>
    bool CanHandBack,
    // The owning REQUEST's audit trail — the row is keyed on it, and it is what the actions open.
    string? CreatedBy,
    DateTime CreatedOnUtc,
    string? UpdatedBy,
    DateTime UpdatedOnUtc);

/// <summary>
/// The read-only submitted-form view (AC-REMS-013.2/3), rendered from the immutable
/// <c>REMSFormSubmission</c> payload. <see cref="LockedEmail"/> is the request's authoritative customer
/// email (the payload's echoed email is ignored). Distinct from the editable workspace data.
/// </summary>
public sealed record RemsSubmissionView(
    Guid SubmissionId,
    Guid RemsId,
    string RemsNumber,
    string IndustryGroup,
    string? LockedEmail,
    DateTime SubmittedOnUtc,
    RemsFormPayloadV1 Payload);

// -------------------- Workspace read model --------------------

/// <summary>The engagement workspace (AC-REMS-014): the client, its entities, and each entity's engagement.</summary>
public sealed record RemsEngagementWorkspace(
    Guid RemsId,
    string RemsNumber,
    string RequestStatus,
    // Null until the client submits their intake form. The engagement below exists from the moment the
    // request does — the initiator fills its setup before the client is ever contacted — so a workspace
    // with no client is the ordinary state of every request that has not been answered yet.
    RemsClientView? Client,
    IReadOnlyList<RemsEntityView> Entities,
    // The request's single engagement, which used to hang off each entity. Null only before the initiator
    // has saved the request for the first time.
    RemsEngagementView? Engagement,
    // The industry group the client's intake was built around. It lives on the form record rather than the
    // engagement, but the setup section shows and edits it, so it travels with the workspace.
    string? IndustryGroup,
    // Other businesses the client named at intake. Each is a prompt for its own request; a row carrying a
    // CreatedRemsId has already produced one.
    IReadOnlyList<RemsAdditionalEntityView> AdditionalEntities,
    // The tenant's department → director map, so the setup form can show the director a department will
    // get the moment it is picked rather than only after the save round-trip.
    IReadOnlyList<RemsDepartmentDirectorView> DepartmentDirectors);

/// <summary>Another business the client named at intake, and the request it has produced (if any).</summary>
public sealed record RemsAdditionalEntityView(
    Guid Id,
    string FullName,
    string? EmailAddress,
    string? PhoneNumber,
    Guid? CreatedRemsId,
    string? CreatedRemsNumber);

/// <summary>The editable client record. <see cref="Email"/> is locked (never editable).</summary>
public sealed record RemsClientView(
    Guid Id,
    string Name,
    string Email,
    string? MobileNumber,
    string? ReferralSource,
    string? BillingContactName,
    string? BillingEmail);

/// <summary>A shared postal address projected for the workspace (mirrors <see cref="RemsAddressInput"/>).</summary>
public sealed record RemsAddressView(
    Guid Id,
    string? Street,
    string? AddressLine2,
    string? City,
    string? State,
    string? StateCode,
    string? Zip,
    string? CountryCode,
    string? CountryName);

/// <summary>
/// An entity within the workspace, with its addresses and contacts. It no longer carries an engagement:
/// the request has one, reported once on the workspace itself.
/// </summary>
public sealed record RemsEntityView(
    Guid Id,
    string Name,
    string? Ein,
    bool IsMainEntity,
    IReadOnlyList<RemsEntityAddressView> Addresses,
    IReadOnlyList<RemsEntityContactView> Contacts);

/// <summary>An entity address (physical/mailing/billing) row.</summary>
public sealed record RemsEntityAddressView(Guid Id, string AddressType, RemsAddressView Address);

/// <summary>An entity contact row (person resolved to name/email/phone).</summary>
public sealed record RemsEntityContactView(Guid Id, string Role, bool IsRequired, string? Name, string? Email, string? Phone);

/// <summary>An engagement with its team, fee/realization, marketing, commission and conditional details.</summary>
public sealed record RemsEngagementView(
    Guid Id,
    string? Department,
    string? ServiceLine,
    string? SubServiceLine,
    string? SubIndustry,
    RemsUserRef? DepartmentDirector,
    RemsUserRef? EngagementExecutive,
    RemsUserRef? BillingManager,
    decimal? FirstYearFeeEstimate,
    decimal? RealizationPercentage,
    string? BillingPeriod,
    int? NumberOfBills,
    string Status,
    IReadOnlyList<Guid> MarketingMethodIds,
    IReadOnlyList<RemsCommissionSplitView> CommissionSplits,
    RemsAuditDetailView? Audit,
    RemsGovernmentDetailView? Government,
    RemsTaxDetailView? Tax);

/// <summary>A commission split (employee + percentage).</summary>
public sealed record RemsCommissionSplitView(Guid Id, RemsUserRef Employee, decimal Percentage);

/// <summary>Audit engagement detail: the linked signed client-acceptance-form media.</summary>
public sealed record RemsAuditDetailView(Guid Id, Guid? ClientAcceptanceFormMediaId);

/// <summary>Government audit detail: contract number, Florida 1% flag, and the copied contract/PO dates.</summary>
public sealed record RemsGovernmentDetailView(
    Guid Id,
    string? ContractNumber,
    bool? FloridaOnePercentStateFeeApplies,
    DateOnly? ContractStartDate,
    DateOnly? ContractEndDate,
    string? OriginalTerm,
    string? RenewalTerms,
    DateOnly? PurchaseOrderStartDate,
    DateOnly? PurchaseOrderEndDate);

/// <summary>Tax engagement detail: fiscal year end, calculated due dates (JSON), and the form checklist.</summary>
public sealed record RemsTaxDetailView(
    Guid Id,
    DateOnly? FiscalYearEnd,
    string? CalculatedDueDates,
    IReadOnlyList<Guid> TaxFormIds);

// -------------------- Editing requests --------------------

/// <summary>
/// A postal address input node (all lines optional; null/all-blank clears where allowed). Same shape as
/// the public form's <see cref="RemsAddressPayload"/> so one field-set drives both screens: line 1 is
/// <see cref="Street"/>, the state carries both its display name and ISO code, and the country comes from
/// the client's country → state → city cascade.
/// </summary>
public sealed class RemsAddressInput
{
    public string? Street { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? StateCode { get; set; }
    public string? Zip { get; set; }
    public string? CountryCode { get; set; }
    public string? CountryName { get; set; }

    /// <summary>Content in any postal line. The country is excluded — it is pre-selected on a blank address.</summary>
    public bool HasAny =>
        !string.IsNullOrWhiteSpace(Street) || !string.IsNullOrWhiteSpace(AddressLine2)
        || !string.IsNullOrWhiteSpace(City) || !string.IsNullOrWhiteSpace(State)
        || !string.IsNullOrWhiteSpace(Zip);
}

/// <summary>Update the client record (AC-REMS-014). The client email is locked and can never be changed.</summary>
public sealed class UpdateRemsClientRequest
{
    public string? Name { get; set; }
    public string? MobileNumber { get; set; }
    public string? ReferralSource { get; set; }
    public string? BillingContactName { get; set; }
    public string? BillingEmail { get; set; }
    public RemsAddressInput? BillingAddress { get; set; }
}

/// <summary>Replace an entity's physical/mailing addresses (each null =&gt; remove that address type).</summary>
public sealed class UpdateRemsEntityAddressesRequest
{
    public RemsAddressInput? PhysicalAddress { get; set; }
    public RemsAddressInput? MailingAddress { get; set; }
}

/// <summary>Replace an entity's contacts (AC-REMS-014). Each contact is upserted by its role.</summary>
public sealed class UpdateRemsEntityContactsRequest
{
    public List<RemsEntityContactInput> Contacts { get; set; } = new();
}

/// <summary>A single entity contact input (role + person name/email/phone).</summary>
public sealed class RemsEntityContactInput
{
    public string Role { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsRequired { get; set; }
}

/// <summary>
/// Update an engagement's team, service placement and fee/realization (AC-REMS-014). Null fields are left
/// unchanged. Setting <see cref="Department"/> prefills the department director from the tenant mapping
/// unless <see cref="DepartmentDirectorId"/> is supplied (staff override).
/// </summary>
public sealed class UpdateRemsEngagementRequest
{
    public string? Department { get; set; }

    /// <summary>
    /// RETIRED, and still accepted only so an old caller is not broken by sending it. The setup form drops
    /// it from the payload entirely, which is what preserves whatever a historical engagement recorded.
    /// </summary>
    public string? ServiceLine { get; set; }

    /// <summary>
    /// The service being sold — the SERVICE LINE as the setup form labels it (option-set
    /// <c>REMS.SubServiceLine</c> code; the key kept its old name). Optional, so — like the client's own
    /// optional fields — an EMPTY string clears it while null leaves it alone.
    /// </summary>
    public string? SubServiceLine { get; set; }

    /// <summary>
    /// The client's trade — the INDUSTRY as the setup form labels it (option-set <c>REMS.SubIndustry</c>
    /// code, likewise). Cleared with an empty string, as above.
    /// </summary>
    public string? SubIndustry { get; set; }

    public Guid? DepartmentDirectorId { get; set; }
    public Guid? EngagementExecutiveId { get; set; }
    public Guid? BillingManagerId { get; set; }
    public decimal? FirstYearFeeEstimate { get; set; }
    public decimal? RealizationPercentage { get; set; }

    /// <summary>How often the client is billed (option-set <c>REMS.BillingPeriod</c> code).</summary>
    public string? BillingPeriod { get; set; }

    /// <summary>How many bills the engagement is billed over. Entered, not derived from the period.</summary>
    public int? NumberOfBills { get; set; }
}

/// <summary>The engagement update result: the refreshed engagement plus the director the chosen department maps to (prefill hint).</summary>
public sealed record RemsEngagementUpdateResult(RemsEngagementView Engagement, Guid? MappedDepartmentDirectorId);

/// <summary>Link a previously-uploaded media id as the signed client-acceptance form (AC-REMS-014.12).</summary>
public sealed class LinkClientAcceptanceFormRequest
{
    public Guid MediaId { get; set; }
}

/// <summary>Set the government-audit contract detail (AC-REMS-014.13).</summary>
public sealed class UpdateRemsGovernmentDetailRequest
{
    public string? ContractNumber { get; set; }
    public bool? FloridaOnePercentStateFeeApplies { get; set; }
    public DateOnly? ContractStartDate { get; set; }
    public DateOnly? ContractEndDate { get; set; }
    public string? OriginalTerm { get; set; }
    public string? RenewalTerms { get; set; }
    public DateOnly? PurchaseOrderStartDate { get; set; }
    public DateOnly? PurchaseOrderEndDate { get; set; }
}

/// <summary>Set the tax detail: fiscal year end (due dates are recomputed) + the tax-form checklist (AC-REMS-014.14).</summary>
public sealed class UpdateRemsTaxDetailRequest
{
    public DateOnly? FiscalYearEnd { get; set; }
    public List<Guid> TaxFormIds { get; set; } = new();
}

// -------------------- Part B: marketing + commission --------------------

/// <summary>Set the engagement marketing tags (AC-REMS-017): at least one REMS marketing option id is required.</summary>
public sealed class SetRemsMarketingRequest
{
    public List<Guid> MarketingMethodIds { get; set; } = new();
}

/// <summary>
/// Set the engagement commission splits (AC-REMS-016): up to ten recipients, each &gt; 0 and &lt;= 100,
/// allocating no more than 100% in total.
/// </summary>
public sealed class SetRemsCommissionRequest
{
    public List<RemsCommissionInput> Splits { get; set; } = new();
}

/// <summary>A single commission recipient input.</summary>
public sealed class RemsCommissionInput
{
    public Guid EmployeeId { get; set; }
    public decimal Percentage { get; set; }
}

// -------------------- Shared helpers --------------------

/// <summary>
/// The canonical engagement Department / entity-type codes this WO branches on (seeded in
/// <c>DefaultOptionSets</c>). Option-set values are stored as codes and not otherwise validated at save.
/// </summary>
internal static class RemsEngagementCodes
{
    public const string DepartmentAudit = "audit";
    public const string DepartmentTax = "tax";

    /// <summary>
    /// The <c>REMS.IndustryGroup</c> code shown as Entity Type = "Government". A government AUDIT used to
    /// be read off the engagement's service line; that list was dropped for asking what the entity type
    /// already answers, so the rule now reads the entity type itself.
    /// </summary>
    public const string EntityTypeGovernment = "government";

    public static bool IsAudit(string? department)
        => string.Equals(department, DepartmentAudit, StringComparison.OrdinalIgnoreCase);

    public static bool IsTax(string? department)
        => string.Equals(department, DepartmentTax, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// An audit engagement for a government entity — the one that additionally needs a contract number and
    /// the Florida 1% state-fee flag. <paramref name="entityType"/> is the request's
    /// <c>REMSForm.IndustryGroup</c>, not a field of the engagement: it is fixed when the intake form goes
    /// out, which is exactly the guarantee this rule wants.
    /// </summary>
    public static bool IsGovernmentAudit(string? department, string? entityType)
        => IsAudit(department) && string.Equals(entityType, EntityTypeGovernment, StringComparison.OrdinalIgnoreCase);
}

/// <summary>The computed tax due-date schedule stored as JSON on <c>REMSEngagementTaxDetail.CalculatedDueDates</c>.</summary>
public sealed record RemsTaxDueDateSet(DateOnly FiscalYearEnd, DateOnly OriginalDueDate, DateOnly ExtendedDueDate);

/// <summary>
/// Derives a simple, documented tax due-date schedule from a fiscal year end: the original return is due
/// on the 15th day of the 4th month following the fiscal year-end month (e.g. FYE 31 Dec =&gt; 15 Apr), and
/// the extended deadline is six months after that (=&gt; 15 Oct). Stored as JSON for the tax detail.
/// </summary>
internal static class RemsTaxDueDates
{
    public static RemsTaxDueDateSet Compute(DateOnly fiscalYearEnd)
    {
        var monthStart = new DateOnly(fiscalYearEnd.Year, fiscalYearEnd.Month, 1);
        var fourthMonth = monthStart.AddMonths(4);
        var originalDue = new DateOnly(fourthMonth.Year, fourthMonth.Month, 15);
        var extendedDue = originalDue.AddMonths(6);
        return new RemsTaxDueDateSet(fiscalYearEnd, originalDue, extendedDue);
    }

    public static string ComputeJson(DateOnly fiscalYearEnd)
        => JsonSerializer.Serialize(Compute(fiscalYearEnd), Options);

    /// <summary>
    /// Reads back a stored schedule. The STORED value wins over recomputing from the fiscal year end, so a
    /// row keeps the dates it was saved with even if the rule above is ever changed. Unreadable JSON is
    /// treated as absent rather than throwing on a read-only review screen.
    /// </summary>
    public static RemsTaxDueDateSet? TryDeserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RemsTaxDueDateSet>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
