namespace EmsPortal.Api.Models.Rems;

/// <summary>
/// Create payload for a REMS request (WO-111). Provide either an existing-client reference
/// (<see cref="ExistingClientReferenceId"/>, resolved from the client lookup) or a brand-new client —
/// either way <see cref="ClientName"/> is required, as is <see cref="Title"/> and one of
/// <see cref="CustomerEmail"/>/<see cref="CustomerMobileNumber"/> (AC-REMS-004.7).
/// </summary>
public sealed class CreateRemsRequestRequest
{
    /// <summary>Loose reference to an existing client (Person id) when the type is existing/subsidiary.</summary>
    public Guid? ExistingClientReferenceId { get; set; }

    /// <summary>Client name at intake (required — filled from the selected person or free text).</summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>Request type (option-set <c>REMS.Type</c> code, e.g. <c>brand_new_client</c>).</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Request priority (option-set <c>REMS.Priority</c> code, e.g. <c>medium</c>).</summary>
    public string Priority { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public string? CustomerEmail { get; set; }
    public string? CustomerMobileNumber { get; set; }

    /// <summary>Optional Client Service Executive (User id).</summary>
    public Guid? CSEId { get; set; }

    /// <summary>Optional single attachment: a previously-uploaded media id (POST /api/media).</summary>
    public Guid? MediaId { get; set; }

    /// <summary>When true the request is submitted to the Admin Pool; otherwise it is saved as a draft.</summary>
    public bool Submit { get; set; }

    /// <summary>Optional admin (User id) to assign the request to at creation.</summary>
    public Guid? AssignAdminUserId { get; set; }
}

/// <summary>Edit payload for a REMS request (WO-111). Null fields are left unchanged.</summary>
public sealed class UpdateRemsRequestRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Type { get; set; }
    public string? Priority { get; set; }
    public string? ClientName { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerMobileNumber { get; set; }
    public Guid? CSEId { get; set; }
    public Guid? ExistingClientReferenceId { get; set; }

    /// <summary>When true a still-draft request is submitted to the Admin Pool as part of this edit.</summary>
    public bool Submit { get; set; }
}

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
    string Title,
    string ClientName,
    string Type,
    string Priority,
    DateTime CreatedOnUtc,
    string Status,
    RemsUserRef? AssignedAdmin,
    RemsUserRef? Cse,
    string? IndustryGroup,
    string EmsFormState,
    string? ClientSubmissionState,
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
    string Title,
    string? Description,
    string ClientName,
    string Type,
    string Priority,
    string Status,
    string? CustomerEmail,
    string? CustomerMobileNumber,
    Guid? ExistingClientReferenceId,
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
    RemsRowActions Actions);

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
