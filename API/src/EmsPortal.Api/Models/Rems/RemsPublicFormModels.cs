using System.Text.Json;

namespace EmsPortal.Api.Models.Rems;

// ---------------------------------------------------------------------------------------------------
// WO-113 — REMS public client EMS form. The versioned wire payload (REMSFormPayloadV1) that backs the
// auto-save draft and the immutable submission, plus the public load-state, review-presentation and
// acknowledgement models. All types are intentionally lenient (nullable) so a partial draft round-trips;
// the industry-group rules are enforced by RemsFormPayloadValidator at review + submit, not by binding.
// ---------------------------------------------------------------------------------------------------

/// <summary>
/// Versioned REMS onboarding form payload (V1). Stored verbatim as the <c>REMSFormDraft.DraftPayload</c>
/// and, on submit, the immutable <c>REMSFormSubmission.SubmittedPayload</c>. <see cref="Email"/> is a
/// courtesy echo only — it is LOCKED to the request's customer email and ignored on submit.
/// </summary>
public sealed class RemsFormPayloadV1
{
    /// <summary>Payload schema version; pinned so future shapes can be migrated.</summary>
    public int Version { get; set; } = 1;

    // ---- Common ----
    public string? ClientName { get; set; }

    /// <summary>Echo of the client email. LOCKED — ignored on submit (the request's customer email wins).</summary>
    public string? Email { get; set; }

    public string? MobileNumber { get; set; }
    public string? ReferralSource { get; set; }

    /// <summary>Free-text follow-up for the chosen referral source, e.g. who referred them.</summary>
    public string? ReferralSourceDetail { get; set; }

    // ---- Address (main entity) ----
    public RemsAddressPayload? PhysicalAddress { get; set; }
    public bool MailingDiffers { get; set; }
    public RemsAddressPayload? MailingAddress { get; set; }

    // ---- Billing ----
    public string? BillingContactName { get; set; }
    public string? BillingEmail { get; set; }
    public RemsAddressPayload? BillingAddress { get; set; }

    // ---- Individual ----
    public string? SpouseName { get; set; }
    public string? SpousePhone { get; set; }
    public string? SpouseEmail { get; set; }

    // ---- Business ----
    public string? Ein { get; set; }

    // ---- Government (contract details) ----
    public DateOnly? ContractStartDate { get; set; }
    public DateOnly? ContractEndDate { get; set; }
    public string? OriginalTerm { get; set; }
    public string? RenewalTerms { get; set; }
    public DateOnly? PoStartDate { get; set; }
    public DateOnly? PoEndDate { get; set; }

    /// <summary>The role contacts collected for the main entity; which are required depends on the industry group.</summary>
    public RemsRolesPayload? Roles { get; set; }

    /// <summary>Related / subsidiary entities captured alongside the main entity.</summary>
    public List<RemsRelatedEntityPayload> RelatedEntities { get; set; } = new();
}

/// <summary>
/// A postal address node in <see cref="RemsFormPayloadV1"/>. The client picks country → state → city from
/// a dependent cascade, so each level carries both the display name (persisted onto <c>Address</c> and
/// shown on review) and, where the source data has one, its ISO code. Every field stays nullable: drafts
/// saved before the cascade existed carry only the four postal lines and must still round-trip.
/// </summary>
public sealed class RemsAddressPayload
{
    /// <summary>Address line 1. Named "street" because the stored payloads are keyed on it.</summary>
    public string? Street { get; set; }

    /// <summary>Address line 2 (optional — the only line of the standard block that never is required).</summary>
    public string? AddressLine2 { get; set; }

    public string? City { get; set; }

    /// <summary>State / province display NAME (e.g. "California"); <see cref="StateCode"/> holds the ISO code.</summary>
    public string? State { get; set; }
    public string? Zip { get; set; }

    /// <summary>ISO-3166-1 alpha-2 country code (e.g. "US").</summary>
    public string? CountryCode { get; set; }

    /// <summary>Country display name resolved from <see cref="CountryCode"/> (e.g. "United States").</summary>
    public string? CountryName { get; set; }

    /// <summary>ISO-3166-2 subdivision code for <see cref="State"/>; null when the country has no state list.</summary>
    public string? StateCode { get; set; }

    /// <summary>
    /// True when at least one postal line carries content (an all-blank node is treated as absent). The
    /// country is deliberately NOT counted: it is pre-selected on every blank address, so on its own it
    /// does not make an address present.
    /// </summary>
    public bool HasAny =>
        !string.IsNullOrWhiteSpace(Street) || !string.IsNullOrWhiteSpace(AddressLine2)
        || !string.IsNullOrWhiteSpace(City) || !string.IsNullOrWhiteSpace(State)
        || !string.IsNullOrWhiteSpace(Zip);
}

/// <summary>A single role contact (name / email / phone). All three are required when the role is required.</summary>
public sealed class RemsRolePayload
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }

    /// <summary>True when any of the three fields carries content (an all-blank role is treated as absent).</summary>
    public bool HasAny =>
        !string.IsNullOrWhiteSpace(Name) || !string.IsNullOrWhiteSpace(Email) || !string.IsNullOrWhiteSpace(Phone);
}

/// <summary>The role contacts, keyed by the canonical <c>RemsContactRole</c> names.</summary>
public sealed class RemsRolesPayload
{
    // Individual
    public RemsRolePayload? Self { get; set; }
    public RemsRolePayload? Spouse { get; set; }

    // Business
    public RemsRolePayload? Ceo { get; set; }
    public RemsRolePayload? Cfo { get; set; }
    public RemsRolePayload? AccountsPayable { get; set; }
    public RemsRolePayload? Banker { get; set; }
    public RemsRolePayload? Lawyer { get; set; }

    // Government
    public RemsRolePayload? FinanceDirector { get; set; }
}

/// <summary>A related / subsidiary entity node.</summary>
public sealed class RemsRelatedEntityPayload
{
    /// <summary>Stable client-supplied key that ties this entity to its payload node (never trusted as an id).</summary>
    public string? SourceKey { get; set; }
    public string? BusinessName { get; set; }
    public string? Ein { get; set; }
    public string? ContactName { get; set; }
    public RemsAddressPayload? PhysicalAddress { get; set; }
    public RemsAddressPayload? MailingAddress { get; set; }
}

// ---------------------------------------------------------------------------------------------------
// Load state (GET) — the public link resolves to exactly one of these states, disclosing nothing about
// requests other than this form's own prefill / draft.
// ---------------------------------------------------------------------------------------------------

/// <summary>The public form state names returned by the GET load endpoint.</summary>
public static class RemsPublicFormStates
{
    /// <summary>Unknown / bad invite code (generic; no disclosure).</summary>
    public const string Invalid = "Invalid";

    /// <summary>The link is not (or no longer) active: request deleted, form cancelled, or not yet sent.</summary>
    public const string Unavailable = "Unavailable";

    /// <summary>Already submitted — a personalized thank-you with no editable data and no reset path.</summary>
    public const string Submitted = "Submitted";

    /// <summary>Live and editable — carries the industry group, locked prefill and any saved draft.</summary>
    public const string Editable = "Editable";
}

/// <summary>
/// The public form load response. Only <see cref="State"/> is always present; the remaining fields are
/// populated per state (thank-you name for Submitted; industry group + prefill + draft for Editable).
/// </summary>
public sealed record RemsPublicFormResponse(
    string State,
    string? ClientName = null,
    string? IndustryGroup = null,
    RemsPublicPrefill? Prefill = null,
    RemsFormPayloadV1? DraftPayload = null,
    // The tenant's REMS.ReferralSource list, delivered WITH the form. This page is anonymous — the
    // client holds an invite code, not a session — so it cannot call the authenticated option-set
    // resolve endpoint the staff screens use. Sending the resolved list here is what lets the picker
    // honour a tenant's own wording and descriptions instead of falling back to a hardcoded copy.
    IReadOnlyList<RemsPublicOption>? ReferralSources = null);

/// <summary>
/// One selectable value for a public-form picker. <see cref="Description"/> is the option item's own
/// description, rendered as the value's tooltip; null when the tenant has not written one.
/// </summary>
public sealed record RemsPublicOption(string Value, string Label, string? Description);

/// <summary>Prefill for the editable form. <see cref="Email"/> is display-locked (from the request, not editable).</summary>
public sealed record RemsPublicPrefill(string? ClientName, string Email, string? MobileNumber);

/// <summary>Draft auto-save acknowledgement (WO-113 PUT draft).</summary>
public sealed record RemsDraftSavedResponse(DateTime LastSavedOnUtc);

/// <summary>Client-cancellation acknowledgement (WO-113 POST cancel) — non-destructive; the draft is kept.</summary>
public sealed record RemsPublicCancelResponse(bool Acknowledged);

// ---------------------------------------------------------------------------------------------------
// Review presentation model (POST review) — read-only, grouped EXACTLY as AC-REMS-024.7:
// Contact · Contract Details (Government only) · Other Entities · Address · Additional Contacts · Billing.
// ---------------------------------------------------------------------------------------------------

/// <summary>The read-only review presentation model (AC-REMS-024.7).</summary>
public sealed record RemsReviewModel(
    RemsReviewContact Contact,
    RemsReviewContractDetails? ContractDetails,
    IReadOnlyList<RemsReviewOtherEntity> OtherEntities,
    RemsReviewAddressGroup Address,
    IReadOnlyList<RemsReviewContactRow> AdditionalContacts,
    RemsReviewBilling Billing);

/// <summary>Primary client contact. <see cref="Email"/> is the locked request email.</summary>
public sealed record RemsReviewContact(string? ClientName, string Email, string? MobileNumber, string? ReferralSource);

/// <summary>Government contract details block.</summary>
public sealed record RemsReviewContractDetails(
    DateOnly? ContractStartDate,
    DateOnly? ContractEndDate,
    string? OriginalTerm,
    string? RenewalTerms,
    DateOnly? PurchaseOrderStartDate,
    DateOnly? PurchaseOrderEndDate);

/// <summary>A related entity as shown on review.</summary>
public sealed record RemsReviewOtherEntity(
    string? SourceKey,
    string? BusinessName,
    string? Ein,
    string? ContactName,
    RemsAddressPayload? PhysicalAddress,
    RemsAddressPayload? MailingAddress);

/// <summary>The main entity's physical + (optional) mailing address.</summary>
public sealed record RemsReviewAddressGroup(RemsAddressPayload? Physical, bool MailingDiffers, RemsAddressPayload? Mailing);

/// <summary>A role contact row on review.</summary>
public sealed record RemsReviewContactRow(string Role, bool IsRequired, string? Name, string? Email, string? Phone);

/// <summary>Billing block.</summary>
public sealed record RemsReviewBilling(string? BillingContactName, string? BillingEmail, RemsAddressPayload? BillingAddress);

/// <summary>Shared JSON options for (de)serializing the stored draft / submission payloads (web defaults: camelCase, case-insensitive).</summary>
public static class RemsFormPayloadJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(RemsFormPayloadV1 payload) => JsonSerializer.Serialize(payload, Options);

    public static RemsFormPayloadV1? TryDeserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RemsFormPayloadV1>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
