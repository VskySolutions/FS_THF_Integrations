namespace EmsPortal.Api.Models.Rems;

/// <summary>
/// Build/save the EMS onboarding form for a REMS request (WO-112, AC-REMS-007). Both the CSE and the
/// industry group are required before the form can be saved (AC-REMS-007.7); changing the industry group
/// before the form is sent regenerates the invite link (AC-REMS-007.5), and both are locked once sent.
/// </summary>
public sealed class SaveRemsFormRequest
{
    /// <summary>The Client Service Executive to assign to the request (User id) — required.</summary>
    public Guid CseUserId { get; set; }

    /// <summary>Industry group (option-set <c>REMS.IndustryGroup</c> code: individual/business/government) — required.</summary>
    public string IndustryGroup { get; set; } = string.Empty;
}

/// <summary>The current EMS form state on a request (WO-112). Null on <see cref="RemsFormBuildScreen.Form"/> when no form has been built yet.</summary>
public sealed record RemsFormInfo(
    Guid Id,
    string IndustryGroup,
    string InviteCode,
    string FormLink,
    string Status,
    DateTime? SentOnUtc,
    DateTime? SubmittedOnUtc,
    DateTime? InviteLockedOnUtc,
    bool IsLocked);

/// <summary>
/// The EMS form build-screen model (WO-112, AC-REMS-007.1): the REMS request context plus the current
/// form (if any). The CSE lives on the request (<c>REMS.CSEId</c>); the invite code / link / status live
/// on the form.
/// </summary>
public sealed record RemsFormBuildScreen(
    Guid RemsId,
    string RemsNumber,
    string RequestTitle,
    string ClientName,
    string RequestStatus,
    string? CustomerEmail,
    string? CustomerMobileNumber,
    RemsUserRef? Cse,
    RemsFormInfo? Form);

/// <summary>The pre-send preview (WO-112, AC-REMS-008.1): where the form link will be emailed, and the link itself.</summary>
public sealed record RemsFormPreview(string? DestinationEmail, string FormLink);

/// <summary>
/// A single email-delivery event row in the form email log (WO-112, AC-REMS-008.6), newest first.
/// <paramref name="Detail"/> explains a Failed event this portal recorded itself (no SMTP account,
/// rejected credentials, …) and is null for everything else — raw provider payloads are never surfaced.
/// </summary>
public sealed record RemsEmailEventRow(
    Guid Id,
    string EventType,
    string RecipientEmail,
    DateTime OccurredOnUtc,
    string? ProviderMessageId,
    string? Detail);

/// <summary>
/// One EMS-Inbox row (WO-112, AC-REMS-009): a request with a form, its request context, form state,
/// creator, and the latest send/delivery/open info.
/// </summary>
public sealed record RemsInboxRow(
    Guid RemsId,
    string RemsNumber,
    string ClientName,
    string EngagementType,
    string RequestStatus,
    string FormStatus,
    RemsUserRef? FormCreatedBy,
    DateTime? FormSentOnUtc,
    string? LatestEmailEventType,
    DateTime? LatestEmailEventOnUtc);
