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
    string ClientName,
    string RequestStatus,
    string? CustomerEmail,
    string? CustomerMobileNumber,
    RemsUserRef? Cse,
    RemsFormInfo? Form);

/// <summary>The pre-send preview (WO-112, AC-REMS-008.1): where the form link will be emailed, and the link itself.</summary>
public sealed record RemsFormPreview(
    string? DestinationEmail,
    string FormLink,
    // The effective template rendered with this request's values — exactly what the client would receive
    // if the admin sent without touching it. Null when the tenant has no effective RemsFormLink template,
    // which is also why nothing would be sent.
    string? Subject,
    string? Body);

/// <summary>Send payload: the subject / body as the admin left them in the send dialog.</summary>
public sealed class SendRemsFormRequest
{
    public string? Subject { get; set; }
    public string? Body { get; set; }
}

/// <summary>
/// A single email-delivery event row in the form email log (WO-112, AC-REMS-008.6), newest first.
/// <paramref name="Detail"/> explains a Failed event this portal recorded itself (no SMTP account,
/// rejected credentials, …) and is null for everything else — raw provider payloads are never surfaced.
/// <para>
/// <paramref name="Subject"/> and <paramref name="Body"/> are the message as it was sent, present on the
/// rows this portal raised (Sent, Reminder) and null on provider callbacks and on anything sent before
/// they were recorded. The <c>ProviderMessageId</c> is deliberately NOT on this record: it is a transport
/// identifier of the form <c>…@localhost</c>, meaningless to the reader, and the message itself is what
/// they came to see.
/// </para>
/// </summary>
public sealed record RemsEmailEventRow(
    Guid Id,
    string EventType,
    string RecipientEmail,
    DateTime OccurredOnUtc,
    string? Detail,
    // Who pressed Send or Remind. Null on the events a provider reported back (delivered / opened /
    // failed): nobody here caused those, and "the system" is not a person worth naming.
    string? SentBy,
    string? Subject,
    string? Body);

/// <summary>
/// The email log as one screen: the delivery events, and whether THIS caller can nudge the client from
/// it. <paramref name="CanRemind"/> is the same test the reminder endpoint itself applies — the
/// rems.forms.send permission, the record rule about whose request this is, and the state window a
/// reminder makes sense in — so the button is offered exactly when pressing it would work.
/// <para>
/// <paramref name="RemindBlockedReason"/> explains a refusal only where knowing it changes what the
/// reader would do: the request's own state ("the client has already submitted this"), or its being with
/// somebody else. A caller who simply does not hold rems.forms.send gets null and no button — that they
/// cannot send is not news to them.
/// </para>
/// </summary>
/// <para>
/// <paramref name="ClientFormLink"/> is the client's own intake link, offered for copying in exactly the
/// window where it is theirs to follow: the form has been sent and they have not answered yet. Null before
/// that (the link is dead until the form is Sent, and a staff member opening it first is how a request ends
/// up filled in by the wrong hand) and null after (there is nothing left to fill in).
/// </para>
public sealed record RemsEmailLog(
    bool CanRemind,
    string? RemindBlockedReason,
    IReadOnlyList<RemsEmailEventRow> Events,
    string? ClientFormLink);

/// <summary>
/// One EMS-Inbox row (WO-112, AC-REMS-009): a request with a form, its request context, form state,
/// creator, and the latest send/delivery/open info.
/// </summary>
// Trailing four: the owning request's audit trail, offered as hidden-by-default columns.
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
    DateTime? LatestEmailEventOnUtc,
    // Who picked the request up — engagement setup is theirs, so the list can say so.
    RemsUserRef? AssignedAdmin,
    string? CreatedBy,
    DateTime CreatedOnUtc,
    string? UpdatedBy,
    DateTime UpdatedOnUtc);
