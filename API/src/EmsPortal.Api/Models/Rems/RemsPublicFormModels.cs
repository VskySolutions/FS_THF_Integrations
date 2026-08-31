using System.Text.Json;
using System.Text.Json.Serialization;

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

    /// <summary>
    /// The client's name as one string. For a business or a government body this is the ENTITY name and
    /// the only name asked for; for an individual it is <see cref="ClientFirstName"/> and
    /// <see cref="ClientLastName"/> joined, written alongside them so that everything reading "the
    /// client's name" — the materialised client, the entity, the thank-you page — keeps one field to read.
    /// </summary>
    public string? ClientName { get; set; }

    /// <summary>
    /// The generational particle on an individual client's name — Jr., Sr., II, III, IV. Held beside the
    /// family name rather than typed into it, and deliberately NOT folded into
    /// <see cref="EffectiveClientName"/>: the name is what the client is filed and searched under, and
    /// "John Smith Jr." matches no record when "John Smith" matches the man. Null for a business or
    /// government client, whose name is a company's and carries no such particle.
    /// </summary>
    public string? ClientSuffix { get; set; }

    /// <summary>
    /// The courtesy title an individual client was once asked for — Mr., Mrs., Ms., Dr.
    /// <para>
    /// RETIRED from the form, which asks for the generational <see cref="ClientSuffix"/> instead. Still
    /// read and still round-tripped, because a submission saved while the box asked for a title carries
    /// one, and a submission is the immutable record of what the client sent.
    /// </para>
    /// </summary>
    public string? ClientPrefix { get; set; }

    /// <summary>
    /// An individual client's given name. Null for a business or government client, whose name is a
    /// company's rather than a person's and does not divide into two.
    /// </summary>
    public string? ClientFirstName { get; set; }

    /// <summary>An individual client's family name. Null for a business or government client.</summary>
    public string? ClientLastName { get; set; }

    /// <summary>Echo of the client email. LOCKED — ignored on submit (the request's customer email wins).</summary>
    public string? Email { get; set; }

    public string? MobileNumber { get; set; }
    public string? ReferralSource { get; set; }

    /// <summary>Free-text follow-up for the chosen referral source, e.g. who referred them.</summary>
    public string? ReferralSourceDetail { get; set; }

    // ---- Address (main entity) ----
    public RemsAddressPayload? PhysicalAddress { get; set; }
    public RemsAddressPayload? MailingAddress { get; set; }

    // ---- Billing ----
    public string? BillingContactName { get; set; }
    public string? BillingEmail { get; set; }
    public RemsAddressPayload? BillingAddress { get; set; }

    /// <summary>
    /// Everyone else the invoice should reach. The FIRST billing contact is
    /// <c>Roles.BillingContact</c> — the one an entity type asks for, and the one that is required where
    /// it is required — and these are the rest, in the order the client gave them.
    /// <para>
    /// A list rather than a second and third slot: a client whose accounts payable is four people has
    /// four, and the form should not be the thing that decides they have one. Each becomes a
    /// <c>BillingContact</c> on the entity exactly as the first does, marked not-required — being named
    /// second does not make somebody a different kind of contact.
    /// </para>
    /// </summary>
    public List<RemsRolePayload> AdditionalBillingContacts { get; set; } = new();

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

    /// <summary>
    /// The client's name as it should read: the two boxes joined where an individual gave them, and the
    /// single box otherwise. Derived rather than trusted, so a payload whose <see cref="ClientName"/>
    /// disagrees with its parts is filed under the parts the client actually typed.
    /// </summary>
    [JsonIgnore]
    public string EffectiveClientName
    {
        get
        {
            var joined = string.Join(
                " ",
                new[] { ClientFirstName, ClientLastName }
                    .Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()));
            return joined.Length > 0 ? joined : ClientName?.Trim() ?? string.Empty;
        }
    }

    /// <summary>The roles with the legacy keys folded in — always read the contacts through this.</summary>
    [JsonIgnore]
    public RemsRolesPayload EffectiveRoles => (Roles ?? new RemsRolesPayload()).Normalized();
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

/// <summary>
/// A single role contact. The name is asked as two boxes — <see cref="FirstName"/> and
/// <see cref="LastName"/> — because a contact becomes a <c>Person</c>, and a Person is filed under a
/// given name and a family name. <see cref="Name"/> is the two joined, written alongside them so that
/// everything already reading "the contact's name" keeps one field to read.
/// <para>
/// On a payload saved before the split, <see cref="Name"/> is the ONLY name present. Read it through
/// <see cref="EffectiveFirstName"/> / <see cref="EffectiveLastName"/>, which fall back to splitting it.
/// </para>
/// </summary>
public sealed class RemsRolePayload
{
    /// <summary>
    /// The title this contact is addressed by — Mr., Mrs., Ms., Dr. Kept out of <see cref="DisplayName"/>
    /// for the same reason it is kept out of the client's: the joined name is what the contact's
    /// <c>Person</c> is filed under, and a title is not part of it.
    /// <para>
    /// RETIRED from the form — a contact is asked for its generational <see cref="Suffix"/> instead, and
    /// that is the one particle a <c>Person</c> now has a column for. Still read and still round-tripped,
    /// because a submission saved while the box asked for a title carries one, and a submission is the
    /// immutable record of what the client sent.
    /// </para>
    /// </summary>
    public string? Prefix { get; set; }

    /// <summary>
    /// The generational particle on this contact's name — Jr., Sr., II, III, IV. Out of
    /// <see cref="DisplayName"/> for exactly the reason the prefix is: the joined name is what the
    /// contact's <c>Person</c> is filed and searched under, and "Smith Jr." in a surname column is a
    /// contact nobody finds by searching for their name. It is joined back on in
    /// <see cref="AddressedName"/>, which is what the materialised Person is DISPLAYED as.
    /// </summary>
    public string? Suffix { get; set; }

    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    /// <summary>First and last joined. The single-box answer on payloads saved before the split.</summary>
    public string? Name { get; set; }

    public string? Email { get; set; }
    public string? Phone { get; set; }

    /// <summary>The joined name, from the two boxes when they carry anything and from <see cref="Name"/> otherwise.</summary>
    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            var joined = string.Join(
                " ",
                new[] { FirstName, LastName }.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()));
            return joined.Length > 0 ? joined : Name?.Trim() ?? string.Empty;
        }
    }

    /// <summary>
    /// The joined name with its generational particle on the end — "Jane Smith Jr.". What a materialised
    /// contact's <c>Person.DisplayName</c> is set from: DisplayName is the "as it reads" field, and it is
    /// what every REMS surface shows a contact by. The particle also travels separately into
    /// <c>Person.Suffix</c>, which is where it is EDITED; this is only how it reads.
    /// <para>
    /// The retired PREFIX is deliberately not in here. A Person holds one particle and it is the suffix,
    /// so a courtesy title on an older submission stays in that submission — which is the record of what
    /// the client actually typed — rather than being folded into a name it was never part of.
    /// </para>
    /// <para>
    /// The two name columns stay clean either way, so the person is still filed and found under the name
    /// alone.
    /// </para>
    /// </summary>
    [JsonIgnore]
    public string NameWithSuffix
    {
        get
        {
            var name = DisplayName;
            if (name.Length == 0)
            {
                return string.Empty;
            }

            var suffix = Suffix?.Trim();
            return string.IsNullOrWhiteSpace(suffix) ? name : $"{name} {suffix}";
        }
    }

    /// <summary>The given name, falling back to the first word of a pre-split <see cref="Name"/>.</summary>
    [JsonIgnore]
    public string EffectiveFirstName =>
        !string.IsNullOrWhiteSpace(FirstName) ? FirstName.Trim() : RemsNameSplit.Split(Name).First;

    /// <summary>The family name, falling back to the rest of a pre-split <see cref="Name"/>.</summary>
    [JsonIgnore]
    public string EffectiveLastName =>
        !string.IsNullOrWhiteSpace(LastName) ? LastName.Trim() : RemsNameSplit.Split(Name).Last;

    /// <summary>
    /// True when any field carries content (an all-blank role is treated as absent). Neither particle
    /// counts: a suffix picked out of curiosity beside five empty boxes is not a contact, and treating it
    /// as one would make an otherwise-blank optional role start failing validation as "partly filled".
    /// </summary>
    [JsonIgnore]
    public bool HasAny =>
        !string.IsNullOrWhiteSpace(FirstName) || !string.IsNullOrWhiteSpace(LastName)
        || !string.IsNullOrWhiteSpace(Name) || !string.IsNullOrWhiteSpace(Email)
        || !string.IsNullOrWhiteSpace(Phone);
}

/// <summary>
/// The role contacts, keyed by the canonical <c>RemsContactRole</c> names.
/// <para>
/// The business roles are named for what the firm needs from the person rather than for the office they
/// hold. The old keys — <c>ceo</c>, <c>cfo</c>, <c>accountsPayable</c> — are still READ, because every
/// payload saved before the rename carries them and a client part-way through a form must not lose the
/// contacts they have already typed; <see cref="Normalized"/> folds them into their successors. They are
/// never written back: null legacy keys are dropped on serialize, so a draft re-saved after the rename
/// comes back in the new shape.
/// </para>
/// </summary>
public sealed class RemsRolesPayload
{
    // Individual
    public RemsRolePayload? Self { get; set; }
    public RemsRolePayload? Spouse { get; set; }

    // Business
    public RemsRolePayload? PrimaryContact { get; set; }
    public RemsRolePayload? FinancialContact { get; set; }
    public RemsRolePayload? BillingContact { get; set; }

    /// <summary>Anyone else the client wants the firm to have. Optional, and asked of every non-individual.</summary>
    public RemsRolePayload? OtherContact { get; set; }

    // Government
    public RemsRolePayload? FinanceDirector { get; set; }

    // ---- Legacy keys: read, never written ----

    /// <summary>Legacy key for <see cref="PrimaryContact"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RemsRolePayload? Ceo { get; set; }

    /// <summary>Legacy key for <see cref="FinancialContact"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RemsRolePayload? Cfo { get; set; }

    /// <summary>Legacy key for <see cref="BillingContact"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RemsRolePayload? AccountsPayable { get; set; }

    /// <summary>Retired role, no longer asked for. Present on older payloads only.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RemsRolePayload? Banker { get; set; }

    /// <summary>Retired role, no longer asked for. Present on older payloads only.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RemsRolePayload? Lawyer { get; set; }

    /// <summary>
    /// This node with the legacy keys folded into their successors, so everything downstream reads one
    /// shape. A payload carrying BOTH keeps the current one: it was written later, by a form that offered
    /// the legacy answer nowhere.
    /// </summary>
    public RemsRolesPayload Normalized() => new()
    {
        Self = Self,
        Spouse = Spouse,
        PrimaryContact = Pick(PrimaryContact, Ceo),
        FinancialContact = Pick(FinancialContact, Cfo),
        BillingContact = Pick(BillingContact, AccountsPayable),
        OtherContact = OtherContact,
        FinanceDirector = FinanceDirector,
        Banker = Banker,
        Lawyer = Lawyer,
    };

    private static RemsRolePayload? Pick(RemsRolePayload? current, RemsRolePayload? legacy)
        => current is { HasAny: true } ? current : legacy ?? current;
}

/// <summary>
/// First word is the given name, the rest the family name. The fallback for a name captured as one box —
/// pre-split payloads on the public form, and the single client-name box the staff intake still uses.
/// </summary>
public static class RemsNameSplit
{
    public static (string First, string Last) Split(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return (string.Empty, string.Empty);
        }

        var space = trimmed.IndexOf(' ');
        return space < 0 ? (trimmed, string.Empty) : (trimmed[..space], trimmed[(space + 1)..].Trim());
    }
}

/// <summary>A related / subsidiary entity node.</summary>
public sealed class RemsRelatedEntityPayload
{
    /// <summary>Stable client-supplied key that ties this entity to its payload node (never trusted as an id).</summary>
    public string? SourceKey { get; set; }

    /// <summary>Who to speak to about this other business.</summary>
    public string? FullName { get; set; }
    public string? EmailAddress { get; set; }
    public string? PhoneNumber { get; set; }

    // An additional entity is a CONTACT, not a second legal entity: it produces no REMSEntity, no
    // engagement and no approval round of its own. It becomes its own REMS request, raised by hand from
    // the Partner/CSE list, and that request's own intake is where the business details get asked for.
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

/// <summary>
/// Prefill for the editable form. <see cref="Email"/> is display-locked (from the request, not editable).
/// The name arrives both whole and split: staff intake asks for it in one box, and an individual's form
/// asks for it in two, so the split is done here rather than in the browser — it is the same split the
/// contacts and the client's Person record already get.
/// </summary>
public sealed record RemsPublicPrefill(
    string? ClientName, string? ClientFirstName, string? ClientLastName, string Email, string? MobileNumber);

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

/// <summary>
/// The client themselves. <see cref="Email"/> is the locked request email; the two name parts are
/// populated only for an individual, whose name is a person's rather than a company's.
/// </summary>
public sealed record RemsReviewContact(
    string? ClientName,
    string? ClientSuffix,
    string? ClientFirstName,
    string? ClientLastName,
    string Email,
    string? MobileNumber,
    string? ReferralSource);

/// <summary>Government contract details block.</summary>
public sealed record RemsReviewContractDetails(
    DateOnly? ContractStartDate,
    DateOnly? ContractEndDate,
    string? OriginalTerm,
    string? RenewalTerms,
    DateOnly? PurchaseOrderStartDate,
    DateOnly? PurchaseOrderEndDate);

/// <summary>Another business the client named, as shown on review — a contact, not a second entity.</summary>
public sealed record RemsReviewOtherEntity(
    string? SourceKey,
    string? FullName,
    string? EmailAddress,
    string? PhoneNumber);

/// <summary>The main entity's three addresses. Each is stored in its own right, so none is conditional.</summary>
public sealed record RemsReviewAddressGroup(
    RemsAddressPayload? Physical,
    RemsAddressPayload? Mailing,
    RemsAddressPayload? Billing);

/// <summary>
/// A role contact row on review. <see cref="Name"/> is the two parts joined, for reading; the particles
/// stay beside it. <see cref="Prefix"/> is present only on a contact answered before the form asked for a
/// generational <see cref="Suffix"/> instead.
/// </summary>
public sealed record RemsReviewContactRow(
    string Role, bool IsRequired, string? Prefix, string? Suffix, string? FirstName, string? LastName,
    string? Name, string? Email, string? Phone);

/// <summary>
/// Billing block. <see cref="AdditionalContacts"/> is everyone the client named to invoice BEYOND the
/// first — the first is the <c>billingContact</c> role, listed among the contacts above.
/// </summary>
public sealed record RemsReviewBilling(
    string? BillingContactName,
    string? BillingEmail,
    RemsAddressPayload? BillingAddress,
    IReadOnlyList<RemsReviewContactRow> AdditionalContacts);

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
