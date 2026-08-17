namespace EmsPortal.Api.Models.Rems;

/// <summary>
/// Create payload for a REMS request (WO-111). Provide either an existing-client reference
/// (<see cref="ExistingClientReferenceId"/>, resolved from the client lookup) or a brand-new client —
/// either way <see cref="ClientName"/> is required, as is one of
/// <see cref="CustomerEmail"/>/<see cref="CustomerMobileNumber"/> (AC-REMS-004.7).
/// </summary>
public sealed class CreateRemsRequestRequest
{
    /// <summary>Loose reference to an existing client (Person id) when the type is existing/subsidiary.</summary>
    public Guid? ExistingClientReferenceId { get; set; }

    /// <summary>
    /// The client this one is a subsidiary of (Person id, from the same lookup as the client itself).
    /// Read only when <see cref="Type"/> is the subsidiary code; on any other type it is ignored and the
    /// stored parent is cleared. Must name a person stamped as a client — the type says "child of an
    /// EXISTING client", so a free-text parent would be a contradiction.
    /// </summary>
    public Guid? ParentClientReferenceId { get; set; }

    /// <summary>Client name at intake (required — filled from the selected person or free text).</summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>Request type (option-set <c>REMS.Type</c> code, e.g. <c>brand_new_client</c>).</summary>
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }

    public string? CustomerEmail { get; set; }
    public string? CustomerMobileNumber { get; set; }

    /// <summary>Optional Client Service Executive (User id).</summary>
    public Guid? CSEId { get; set; }

    /// <summary>Optional single attachment: a previously-uploaded media id (POST /api/media).</summary>
    public Guid? MediaId { get; set; }

    // There is no Submit flag any more. A request is always created as a draft: the Admin Pool it used to
    // be submitted into is gone, and what moves a request on is the initiator sending the intake link to
    // the client (POST /api/rems/{id}/form/send).

    /// <summary>
    /// The admin who will review this request once the client's intake comes back. REQUIRED — every
    /// request names its reviewer up front, even though that admin has nothing to do until the client has
    /// answered. Reassignment afterwards is open to both sides.
    /// </summary>
    public Guid AssignAdminUserId { get; set; }

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
    public string? CustomerEmail { get; set; }
    public string? CustomerMobileNumber { get; set; }
    public Guid? CSEId { get; set; }
    public Guid? ExistingClientReferenceId { get; set; }

    /// <summary>
    /// The client this one is a subsidiary of. Unlike every other field here, it is not left alone when
    /// null: it is derived from <see cref="Type"/>, so a request whose type is no longer the subsidiary
    /// code has its parent cleared whatever this says. That is what stops a request switched from
    /// "Subsidiary" to "Brand new client" keeping a parent nothing on the form still shows.
    /// </summary>
    public Guid? ParentClientReferenceId { get; set; }

    /// <summary>
    /// The admin to own this request. A null on its own means "leave the assignment alone", exactly like
    /// every other field on this payload — handing a request back to the pool is said with
    /// <see cref="UnassignAdmin"/>. Changing the assignment additionally requires
    /// <c>rems.requests.assign</c>: editing a request and choosing who works it are separate rights.
    /// </summary>
    public Guid? AssignAdminUserId { get; set; }

    /// <summary>
    /// Clears the assignment. Retained for the rare correction, but a request with no admin has nobody to
    /// review it when the client answers — reassigning to somebody else is almost always what is meant.
    /// Mutually exclusive with <see cref="AssignAdminUserId"/>.
    /// </summary>
    public bool UnassignAdmin { get; set; }
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

/// <summary>Assign (or re-assign) a request to an admin (WO-111, AC-REMS-005).</summary>
public sealed class AssignRemsRequestRequest
{
    public Guid AdminUserId { get; set; }
}

/// <summary>A user reference (id + display name) for the assigned admin / CSE columns.</summary>
public sealed record RemsUserRef(Guid Id, string Name);

/// <summary>Which row actions the caller may perform on a request (drives the dashboard action menu).</summary>
public sealed record RemsRowActions(
    bool CanView,
    bool CanEdit,
    bool CanAssign,
    bool CanDuplicate,
    bool CanDelete);

/// <summary>Dashboard list row for a REMS request (WO-111).</summary>
public sealed record RemsRequestRow(
    Guid Id,
    string RemsNumber,
    string ClientName,
    string Type,
    DateTime CreatedOnUtc,
    string Status,
    // The Admin Pool renders these under the client name and lets you filter on them (its `contact`
    // filter searches exactly these two columns server-side), so the row has to carry them — without
    // them the contact line and the Client Email column could only ever render "—".
    string? CustomerEmail,
    string? CustomerMobileNumber,
    // The client this one is a subsidiary of, for the list's Parent Client column. The name comes off the
    // request row itself, so the column costs the list nothing.
    string? ParentClientName,
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
    string ClientName,
    string Type,
    string Status,
    string? CustomerEmail,
    string? CustomerMobileNumber,
    Guid? ExistingClientReferenceId,
    // The Person master record this request's client is. Set on every save, whether or not intake matched
    // somebody already on file — unlike ExistingClientReferenceId, which stays null for a brand-new
    // client. Null only on requests not saved since the column was added.
    Guid? ClientPersonId,
    // The client this one is a subsidiary of. Both null on every other type — see REMS.ParentClientName
    // for why the name travels with the id instead of being joined at read time.
    Guid? ParentClientReferenceId,
    string? ParentClientName,
    RemsUserRef? AssignedAdmin,
    RemsUserRef? Cse,
    string? IndustryGroup,
    string EmsFormState,
    string? ClientSubmissionState,
    IReadOnlyList<RemsFileRef> Files,
    string? CreatedBy,
    DateTime CreatedOnUtc,
    string? UpdatedBy,
    DateTime UpdatedOnUtc,
    RemsRowActions Actions,
    /// <summary>
    /// The client's own intake link, for copying — non-null only while it is theirs to follow: the form
    /// has been sent and they have not answered yet. Withheld before that because the public endpoint
    /// reports the form unavailable until it is Sent, and because a staff member opening it first is how a
    /// request ends up filled in by the wrong hand; withheld after, because there is nothing left to fill
    /// in. Same window and same reasoning as the Email Log's copy of it.
    /// </summary>
    string? ClientFormLink);

/// <summary>
/// A client-lookup result (WO-111). <see cref="ParentCompany"/> and <see cref="PastWork"/> are always
/// null: no external client directory exists in this platform, so the lookup runs over Person records
/// which carry no such fields.
/// </summary>
public sealed record RemsClientLookupItem(
    Guid Id,
    string Name,
    string? Email,
    string? Phone,
    string? ParentCompany,
    string? PastWork);

/// <summary>An option in the assign-to-admin dropdown (WO-111).</summary>
public sealed record RemsAdminOption(Guid Id, string Name, string? Email);
