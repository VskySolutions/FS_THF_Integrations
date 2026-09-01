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
    /// <summary>The client's name as it reads — the suffix in front of the requested name.</summary>
    string ClientName,
    /// <summary>The two halves of that name, so the Client column can draw the particle in bold.</summary>
    string RequestedClientName,
    string? ClientNameSuffix,
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
/// The submitted-form view (AC-REMS-013.2/3), rendered from the <c>REMSFormSubmission</c> payload.
/// <see cref="LockedEmail"/> is the request's authoritative customer email (the payload's echoed email is
/// ignored). Distinct from the editable workspace data.
/// <para>
/// The snapshot is the client's own answers and is read-only to everybody EXCEPT an Admin, who may correct
/// them in place — a client who typed a digit wrong in their EIN should not have to be sent a second form.
/// A correction overwrites the stored payload rather than filing a second submission, so
/// <see cref="EditedBy"/> / <see cref="EditedOnUtc"/> are how a reader tells a corrected snapshot from one
/// still exactly as it arrived. Both are null on an untouched submission.
/// </para>
/// </summary>
public sealed record RemsSubmissionView(
    Guid SubmissionId,
    Guid RemsId,
    string RemsNumber,
    string IndustryGroup,
    string? LockedEmail,
    /// <summary>
    /// The request's generational suffix on the client's name. The payload does not carry one — the intake
    /// form never asks, because it is the firm's particle on the name rather than something the client
    /// tells us — so a panel rendering the client's own answer needs this to read it as every other REMS
    /// surface does.
    /// </summary>
    string? ClientNameSuffix,
    DateTime SubmittedOnUtc,
    RemsFormPayloadV1 Payload,
    /// <summary>The staff member who last corrected these answers; null while they are the client's own.</summary>
    string? EditedBy = null,
    /// <summary>When they were last corrected; null while they are the client's own.</summary>
    DateTime? EditedOnUtc = null,
    /// <summary>Whether THIS caller may correct them — an Admin, on a request no approval round has frozen.</summary>
    bool CanEdit = false);

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
    // The request's single engagement. Null only before the initiator has saved the request for the
    // first time.
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

/// <summary>
/// A shared postal address projected for the workspace (mirrors <see cref="RemsAddressInput"/>), plus
/// whoever the post is addressed to. The last five are filled only on a billing address, which is the one
/// kind the intake form asks both halves of the question about; a physical or mailing row is a place and
/// nothing more, and carries nulls.
/// </summary>
public sealed record RemsAddressView(
    Guid Id,
    string? Street,
    string? AddressLine2,
    string? City,
    string? State,
    string? StateCode,
    string? Zip,
    string? CountryCode,
    string? CountryName,
    string? Suffix = null,
    string? FirstName = null,
    string? LastName = null,
    string? Email = null,
    string? PhoneNumber = null);

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

/// <summary>
/// An entity contact row (person resolved to name/email/phone). <paramref name="Name"/> is the person's
/// DisplayName, which already reads with the particle in front; <paramref name="Suffix"/> repeats that
/// particle on its own so a surface can draw it in bold rather than hunt for it inside the name.
/// </summary>
public sealed record RemsEntityContactView(
    Guid Id, string Role, bool IsRequired, string? Name, string? Email, string? Phone, string? Suffix = null);

/// <summary>An engagement with its team, fee/realization, marketing, commission and conditional details.</summary>
public sealed record RemsEngagementView(
    Guid Id,
    string? Department,
    string? SubServiceLine,
    string? SubIndustry,
    RemsUserRef? DepartmentDirector,
    RemsUserRef? EngagementExecutive,
    RemsUserRef? BillingManager,
    decimal? FirstYearFeeEstimate,
    decimal? EngagementFee,
    decimal? RealizationPercentage,
    string? BillingPeriod,
    string? BillingProcessDescription,
    string Status,
    IReadOnlyList<Guid> MarketingMethodIds,
    IReadOnlyList<RemsCommissionSplitView> CommissionSplits,
    RemsAuditDetailView? Audit,
    RemsGovernmentDetailView? Government,
    RemsTaxDetailView? Tax);

/// <summary>A commission split (employee + percentage).</summary>
public sealed record RemsCommissionSplitView(Guid Id, RemsUserRef Employee, decimal Percentage);

/// <summary>
/// Attest detail: the linked signed client-acceptance-form media (Audit and Assurance both), plus the
/// three answers Assurance alone is asked — the client's fiscal year end and the administrative fees.
/// </summary>
public sealed record RemsAuditDetailView(
    Guid Id,
    Guid? ClientAcceptanceFormMediaId,
    string? FileName,
    DateOnly? ClientFiscalYearEnd,
    bool? AdminFeesApply,
    decimal? AdminFeesAmount);

/// <summary>
/// Government audit detail — contract number, Florida 1% flag and the copied contract/PO dates — plus the
/// GCS purchase order, which hangs off the SAME PO dates rather than a second copy of them.
/// </summary>
public sealed record RemsGovernmentDetailView(
    Guid Id,
    string? ContractNumber,
    bool? FloridaOnePercentStateFeeApplies,
    DateOnly? ContractStartDate,
    DateOnly? ContractEndDate,
    string? OriginalTerm,
    string? RenewalTerms,
    DateOnly? PurchaseOrderStartDate,
    DateOnly? PurchaseOrderEndDate,
    string? PurchaseOrderNumber,
    decimal? PurchaseOrderAmount,
    Guid? PurchaseOrderMediaId,
    string? PurchaseOrderFileName,
    string? PersonnelLevel,
    decimal? BillRatePerHour);

/// <summary>
/// Tax engagement detail: fiscal year end, the two due dates (derived from it and then editable), the
/// snapshot JSON the approver's packet reads, and the form checklist.
/// </summary>
public sealed record RemsTaxDetailView(
    Guid Id,
    DateOnly? FiscalYearEnd,
    DateOnly? OriginalDueDate,
    DateOnly? FirstExtensionDueDate,
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

    // No billing ADDRESS here. There may be several of them, they are the main entity's rows rather than
    // the client's, and they are written by the client's intake form — this endpoint never touched the
    // single one it used to name.
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

    /// <summary>The Assurance department's fee — its own column, not a relabelled fee estimate.</summary>
    public decimal? EngagementFee { get; set; }

    public decimal? RealizationPercentage { get; set; }

    /// <summary>How often the client is billed (option-set <c>REMS.BillingPeriod</c> code).</summary>
    public string? BillingPeriod { get; set; }

    /// <summary>
    /// How the client is actually billed, in prose. Was a count (No. of Bills); a schedule is a sentence,
    /// not a number. Cleared with an empty string, like the other optional text on this record.
    /// </summary>
    public string? BillingProcessDescription { get; set; }
}

/// <summary>The engagement update result: the refreshed engagement plus the director the chosen department maps to (prefill hint).</summary>
public sealed record RemsEngagementUpdateResult(RemsEngagementView Engagement, Guid? MappedDepartmentDirectorId);

/// <summary>Link a previously-uploaded media id as the signed client-acceptance form (AC-REMS-014.12).</summary>
public sealed class LinkClientAcceptanceFormRequest
{
    public Guid MediaId { get; set; }
}

/// <summary>Link a previously-uploaded media id as the GCS engagement's purchase-order document.</summary>
public sealed class LinkPurchaseOrderRequest
{
    public Guid MediaId { get; set; }
}

/// <summary>
/// Set the ASSURANCE detail: the client's fiscal year end and the administrative fees. The signed
/// client-acceptance form is not here — it is linked by its own endpoint, because it arrives as an upload
/// rather than as a typed field, and Audit engagements use that endpoint too.
/// </summary>
public sealed class UpdateRemsAuditDetailRequest
{
    public DateOnly? ClientFiscalYearEnd { get; set; }
    public bool? AdminFeesApply { get; set; }
    public decimal? AdminFeesAmount { get; set; }
}

/// <summary>Set the government-audit contract detail (AC-REMS-014.13) and the GCS purchase order.</summary>
public sealed class UpdateRemsGovernmentDetailRequest
{
    public string? ContractNumber { get; set; }
    public bool? FloridaOnePercentStateFeeApplies { get; set; }
    public DateOnly? ContractStartDate { get; set; }
    public DateOnly? ContractEndDate { get; set; }
    public string? OriginalTerm { get; set; }
    public string? RenewalTerms { get; set; }

    /// <summary>
    /// The purchase order's dates. Copied here from the client's intake answers for a government entity,
    /// and typed directly by the GCS card — one PO, one pair of dates.
    /// </summary>
    public DateOnly? PurchaseOrderStartDate { get; set; }
    public DateOnly? PurchaseOrderEndDate { get; set; }

    // ---- GCS ----
    public string? PurchaseOrderNumber { get; set; }
    public decimal? PurchaseOrderAmount { get; set; }

    /// <summary>Option-set <c>REMS.PersonnelLevel</c> code.</summary>
    public string? PersonnelLevel { get; set; }
    public decimal? BillRatePerHour { get; set; }
}

/// <summary>
/// Set the tax detail: fiscal year end, the two due dates, and the tax-form checklist (AC-REMS-014.14).
/// A due date left null is DERIVED from the fiscal year end rather than cleared — the rule is the default,
/// not the only answer, so a caller that sends neither still gets the schedule it always got.
/// </summary>
public sealed class UpdateRemsTaxDetailRequest
{
    public DateOnly? FiscalYearEnd { get; set; }
    public DateOnly? OriginalDueDate { get; set; }
    public DateOnly? FirstExtensionDueDate { get; set; }
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
    public const string DepartmentCas = "cas";
    public const string DepartmentGcs = "gcs";

    /// <summary>
    /// Attest work priced for the engagement rather than for its first year. Added beside Audit rather
    /// than in place of it — the two are separate departments with separate directors, and engagements
    /// already filed under <see cref="DepartmentAudit"/> stay exactly where they are.
    /// </summary>
    public const string DepartmentAssurance = "assurance";

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

    public static bool IsCas(string? department)
        => string.Equals(department, DepartmentCas, StringComparison.OrdinalIgnoreCase);

    public static bool IsGcs(string? department)
        => string.Equals(department, DepartmentGcs, StringComparison.OrdinalIgnoreCase);

    public static bool IsAssurance(string? department)
        => string.Equals(department, DepartmentAssurance, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The departments asked for a signed client-acceptance form: Audit and Assurance. The form is the
    /// same compliance artifact under both, so it is stored, uploaded and gated on identically.
    /// </summary>
    public static bool RequiresClientAcceptanceForm(string? department)
        => IsAudit(department) || IsAssurance(department);

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
    /// <summary>The 15th of the fourth month after the fiscal year end.</summary>
    public static DateOnly OriginalDueFor(DateOnly fiscalYearEnd)
    {
        var monthStart = new DateOnly(fiscalYearEnd.Year, fiscalYearEnd.Month, 1);
        var fourthMonth = monthStart.AddMonths(4);
        return new DateOnly(fourthMonth.Year, fourthMonth.Month, 15);
    }

    /// <summary>Six months after the original due date.</summary>
    public static DateOnly FirstExtensionFor(DateOnly originalDue) => originalDue.AddMonths(6);

    public static RemsTaxDueDateSet Compute(DateOnly fiscalYearEnd)
    {
        var originalDue = OriginalDueFor(fiscalYearEnd);
        return new RemsTaxDueDateSet(fiscalYearEnd, originalDue, FirstExtensionFor(originalDue));
    }

    /// <summary>
    /// The schedule as it should actually be recorded: whatever was typed wins, and the rule fills in
    /// only what was left blank. This is what the snapshot JSON is written from, so the approver's packet
    /// reads the dates the engagement was saved with rather than the ones the rule would produce today.
    /// </summary>
    public static RemsTaxDueDateSet Effective(DateOnly fiscalYearEnd, DateOnly? originalDue, DateOnly? firstExtension)
    {
        var original = originalDue ?? OriginalDueFor(fiscalYearEnd);
        return new RemsTaxDueDateSet(fiscalYearEnd, original, firstExtension ?? FirstExtensionFor(original));
    }

    public static string ComputeJson(DateOnly fiscalYearEnd)
        => JsonSerializer.Serialize(Compute(fiscalYearEnd), Options);

    public static string EffectiveJson(DateOnly fiscalYearEnd, DateOnly? originalDue, DateOnly? firstExtension)
        => JsonSerializer.Serialize(Effective(fiscalYearEnd, originalDue, firstExtension), Options);

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
