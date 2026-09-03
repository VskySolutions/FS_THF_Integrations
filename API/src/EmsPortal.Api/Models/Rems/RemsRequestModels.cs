namespace EmsPortal.Api.Models.Rems;

/// <summary>
/// Create payload for a REMS request (WO-111). Provide either an existing-client reference
/// (<see cref="ExistingClientReferenceId"/>, resolved from the client lookup) or a brand-new client —
/// either way <see cref="ClientName"/> is required, as is one of
/// <see cref="CustomerEmail"/>/<see cref="CustomerMobileNumber"/> (AC-REMS-004.7).
/// </summary>
public sealed class CreateRemsRequestRequest
{
    /// <summary>Loose reference to an existing client (Person id) when the referral is for a client THF already has.</summary>
    public Guid? ExistingClientReferenceId { get; set; }

    /// <summary>
    /// The client's name as one string. Still required — it is what a request is identified by — but for
    /// an INDIVIDUAL it is now composed from the two boxes below rather than typed into one.
    /// </summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>
    /// The name in PARTS, for an individual client. The form asks for a first and a last name in two
    /// boxes, so the split no longer has to be guessed — which it never got right for "Van Der Berg".
    /// </summary>
    public string? ClientFirstName { get; set; }

    /// <inheritdoc cref="ClientFirstName"/>
    public string? ClientLastName { get; set; }

    /// <summary>
    /// The legal name, for an ORGANISATION client — every entity type except Individual. Its presence is
    /// what types the client's Person record as an organisation, and what puts the name somewhere other
    /// than a first/last split that was never meant for a company.
    /// </summary>
    public string? ClientCorporateName { get; set; }

    /// <summary>
    /// The generational suffix on that name — Jr., Sr., II, III, IV — kept out of
    /// <see cref="ClientName"/> so the two can be told apart afterwards. Optional and free text; the five
    /// above are offered as suggestions rather than as the whole of what is allowed.
    /// </summary>
    public string? ClientNameSuffix { get; set; }

    /// <summary>Request type (option-set <c>REMS.Type</c> code, e.g. <c>brand_new_client</c>).</summary>
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }

    public string? CustomerEmail { get; set; }
    public string? CustomerMobileNumber { get; set; }

    /// <summary>Optional Client Service Executive (User id).</summary>
    public Guid? CSEId { get; set; }

    /// <summary>Optional single attachment: a previously-uploaded media id (POST /api/media).</summary>
    public Guid? MediaId { get; set; }

    // No Submit flag and no admin to name. A request is always created as a draft; what moves it on is the
    // initiator sending the intake link to the client (POST /api/rems/{id}/form/send), and it reaches every
    // admin's EMS Review unassigned until one picks it up (POST /api/rems/requests/{id}/pick-up).

    /// <summary>
    /// The <c>REMSAdditionalEntity</c> row this request was raised from, when the initiator used the
    /// Create EMS action on another business the client named. Stamping the row is what stops the flag
    /// nagging forever — and what stops the Partner and the CSE, who both watch the same list, each
    /// raising a request for the same entity. The new request itself stands alone: this records where it
    /// came FROM without making it a child of anything.
    /// </summary>
    public Guid? FromAdditionalEntityId { get; set; }
}

/// <summary>Edit payload for a REMS request (WO-111). Null fields are left unchanged.</summary>
public sealed class UpdateRemsRequestRequest
{
    public string? Description { get; set; }
    public string? Type { get; set; }
    public string? ClientName { get; set; }

    /// <inheritdoc cref="CreateRemsRequestRequest.ClientFirstName"/>
    public string? ClientFirstName { get; set; }

    /// <inheritdoc cref="CreateRemsRequestRequest.ClientFirstName"/>
    public string? ClientLastName { get; set; }

    /// <inheritdoc cref="CreateRemsRequestRequest.ClientCorporateName"/>
    public string? ClientCorporateName { get; set; }

    /// <summary>The client's generational suffix. Send <c>""</c> to clear it; omit it to leave it alone.</summary>
    public string? ClientNameSuffix { get; set; }

    public string? CustomerEmail { get; set; }
    public string? CustomerMobileNumber { get; set; }
    public Guid? CSEId { get; set; }
    public Guid? ExistingClientReferenceId { get; set; }

    // Saving a request cannot re-point who reviews it: an admin gains a request by picking it up and loses
    // it by handing it back, both actions of their own rather than a field somebody else writes on an edit.
}

/// <summary>
/// Attach already-uploaded media to an existing request (POST /api/media first). The request form holds
/// several attachments and saves them alongside every other field, so they land after the request exists
/// rather than riding in on the create payload.
/// </summary>
public sealed class AddRemsFilesRequest
{
    public IReadOnlyList<Guid> MediaIds { get; set; } = Array.Empty<Guid>();
}

/// <summary>The Admin's reason for returning a request for rework, and who they are handing it to.</summary>
public sealed class SendBackRemsRequestRequest
{
    /// <summary>Why the setup needs work. Required — a return with no reason is not actionable.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Who owns the rework: <see cref="RemsSendBackTargets.Initiator"/> or
    /// <see cref="RemsSendBackTargets.Cse"/>. Both can already work a returned request, so this decides
    /// who is ASKED rather than who is allowed. Omitted means the initiator, which is where every return
    /// went before the admin was offered the choice.
    /// </summary>
    public string? ReturnTo { get; set; }
}

/// <summary>The two people an admin may hand rework to. Matched case-insensitively on the wire.</summary>
public static class RemsSendBackTargets
{
    public const string Initiator = "initiator";
    public const string Cse = "cse";
}

/// <summary>One return of a request for rework, as history.</summary>
public sealed record RemsSendBackView(
    Guid Id,
    string Reason,
    string? ReturnedBy,
    DateTime ReturnedOnUtc,
    /// <summary>When the initiator handed the revised setup back; null while it is still with them.</summary>
    DateTime? ResolvedOnUtc,
    /// <summary>Who the admin addressed it to, or null on returns made before they were asked to choose.</summary>
    string? ReturnedTo);

/// <summary>A user reference (id + display name) for the assigned admin / CSE columns.</summary>
public sealed record RemsUserRef(Guid Id, string Name);

/// <summary>Which row actions the caller may perform on a request (drives the dashboard action menu).</summary>
public sealed record RemsRowActions(
    bool CanView,
    bool CanEdit,
    /// <summary>
    /// The caller may claim this request as its reviewing admin. True only while nobody holds it — a
    /// request already picked up is never offered to a second admin, so this is "take it", not "take it
    /// off them".
    /// </summary>
    bool CanPickUp,
    bool CanDelete);

/// <summary>Dashboard list row for a REMS request (WO-111).</summary>
public sealed record RemsRequestRow(
    Guid Id,
    string RemsNumber,
    /// <summary>
    /// The client's name as it reads — "Smith John Jr." for a person, the legal name for an organisation.
    /// Surname first: a client list is scanned and sorted by family name.
    /// </summary>
    string ClientName,
    /// <summary>The generational particle, so the Client column can draw it in bold at the end of the name.</summary>
    string? ClientNameSuffix,
    string Type,
    DateTime CreatedOnUtc,
    string Status,
    // The Admin Pool renders these under the client name and lets you filter on them (its `contact`
    // filter searches exactly these two columns server-side), so the row has to carry them — without
    // them the contact line and the Client Email column could only ever render "—".
    string? CustomerEmail,
    string? CustomerMobileNumber,
    RemsUserRef? AssignedAdmin,
    RemsUserRef? Cse,
    string? IndustryGroup,
    string EmsFormState,
    string? ClientSubmissionState,
    // Audit trail, offered as hidden-by-default columns on every list (mirrors RemsRequestDetail).
    string? CreatedBy,
    string? UpdatedBy,
    DateTime UpdatedOnUtc,
    RemsRowActions Actions);

/// <summary>An attached file on a request detail (linked media).</summary>
public sealed record RemsFileRef(
    Guid Id,
    Guid MediaId,
    string? FileName,
    string? MimeType,
    long? FileSize,
    string? Url);

/// <summary>Full REMS request detail (WO-111).</summary>
public sealed record RemsRequestDetail(
    Guid Id,
    string RemsNumber,
    string? Description,
    /// <summary>The client's name as it reads — the suffix after the requested name.</summary>
    string ClientName,
    /// <summary>The generational particle, so a cell can draw it in bold at the end of the name.</summary>
    string? ClientNameSuffix,
    /// <summary>
    /// The name in PARTS, which is how the form asks for it: two boxes for an individual, one for an
    /// organisation. <see cref="ClientName"/> is the composed reading of the same three, for display.
    /// </summary>
    string? ClientFirstName,
    string? ClientLastName,
    string? ClientCorporateName,
    string Type,
    string Status,
    string? CustomerEmail,
    string? CustomerMobileNumber,
    Guid? ExistingClientReferenceId,
    // The Person master record this request's client is. Set on every save, whether or not intake matched
    // somebody already on file — unlike ExistingClientReferenceId, which stays null for a brand-new
    // client. Null only on requests not saved since the column was added.
    Guid? ClientPersonId,
    RemsUserRef? AssignedAdmin,
    RemsUserRef? Cse,
    string? IndustryGroup,
    string EmsFormState,
    string? ClientSubmissionState,
    IReadOnlyList<RemsFileRef> Files,
    RecordAudit Audit,
    RemsRowActions Actions,
    /// <summary>
    /// Whether the send-back dialog may offer the CSE as the person to hand the rework to. True only when
    /// a CSE is named AND the initiator has a REMS delegate in force — the rework is the initiator's own
    /// work, and delegating is how they hand it out. Sent so the dialog offers exactly what the endpoint
    /// will accept, rather than offering a choice that comes back a 400.
    /// </summary>
    bool CanSendBackToCse,
    /// <summary>
    /// The client's own intake link, for copying — non-null only while it is theirs to follow: the form
    /// has been sent and they have not answered yet. Withheld before that because the public endpoint
    /// reports the form unavailable until it is Sent, and because a staff member opening it first is how a
    /// request ends up filled in by the wrong hand; withheld after, because there is nothing left to fill
    /// in. Same window and same reasoning as the Email Log's copy of it.
    /// </summary>
    string? ClientFormLink);

/// <summary>
/// A client-lookup result (WO-111). The lookup runs over Person records — there is no external client
/// directory in this platform — so this is everything the picker can show: who they are, and the two
/// ways of reaching them that the search also matches on.
/// <para>
/// The generational particle on the person's name — Jr., Sr., III — is carried BESIDE the name rather
/// than joined into it. The picker searches on the name, and a record filed as "John Smith Jr." is one
/// nobody finds by typing "John Smith"; but two clients whose names differ only by that particle are two
/// different people, and a list showing both as "John Smith" asks the caller to pick blind. So the name
/// stays the name, and the picker draws the particle after it.
/// </para>
/// </summary>
public sealed record RemsClientLookupItem(
    Guid Id,
    /// <summary>The name as it reads — "Smith John Jr." for a person, the legal name for an organisation.</summary>
    string Name,
    string? Email,
    string? Phone,
    string? Suffix,
    /// <summary>
    /// The name in PARTS, so picking a result can fill the three boxes a person's name is asked in rather
    /// than making the browser split a joined string and guess where the split was. Empty for an
    /// organisation, which has neither.
    /// </summary>
    string FirstName,
    string LastName,
    /// <summary>The legal name, for an organisation. Null for a person.</summary>
    string? CorporateName,
    /// <summary>
    /// Which of the two this is. The picker is already narrowed to one kind by the entity type, so this is
    /// for the result ROW — a company and a person are worth telling apart on sight.
    /// </summary>
    bool IsOrganisation);

/// <summary>An option in the assign-to-admin dropdown (WO-111).</summary>
public sealed record RemsAdminOption(Guid Id, string Name, string? Email);

/// <summary>
/// The client's own details as a request submits them — the four things that used to be columns on
/// <c>REMS</c> and now live on the client's <c>Person</c>.
/// <para>
/// It exists so <c>ResolveClientPersonAsync</c> can be handed what was SUBMITTED rather than reading it
/// back off the request. The request's own name, suffix, email and mobile are read-throughs onto the very
/// Person that method writes, so reading them there would be asking the answer to produce itself.
/// </para>
/// </summary>
public sealed record ClientDetails(
    string? Name,
    string? Suffix,
    string? Email,
    string? Phone,
    /// <summary>
    /// The name in PARTS, for an individual client. The form asks for them in two boxes now, so the
    /// first/last split no longer has to be guessed at — which it never got right for "Van Der Berg".
    /// Null where the form sent only a joined name; <see cref="Name"/> is then split as before.
    /// </summary>
    string? FirstName = null,
    string? LastName = null,
    /// <summary>
    /// The legal name, for an ORGANISATION client. Its presence is what says which kind this is: a
    /// company has no first or last name to put in the two boxes above, and the person record it lands on
    /// is typed <c>Organisation</c> because of this field and nothing else.
    /// </summary>
    string? CorporateName = null)
{
    /// <summary>Whether these details describe a company rather than a human.</summary>
    public bool IsOrganisation => !string.IsNullOrWhiteSpace(CorporateName);
}
