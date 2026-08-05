namespace EmsPortal.Domain.Entities;

/// <summary>
/// The <see cref="REMS.Status"/> option-set codes that make up the request lifecycle (WO-111).
/// <see cref="REMS.Status"/> stores the option item's string VALUE, so these mirror the seeded
/// <c>REMS.Status</c> option list (see <c>DefaultOptionSets</c>).
/// <para>
/// The lifecycle names the STAGE the request is in — i.e. who it is waiting on — rather than the event
/// that last happened to it, so a row's badge answers "what is happening to this request right now":
/// </para>
/// <list type="number">
///   <item><see cref="Draft"/> — with its creator, not yet submitted.</item>
///   <item><see cref="Submitted"/> — in the Admin Pool; an Admin picks it up and builds the EMS form.</item>
///   <item><see cref="AwaitingCustomer"/> — the EMS form has been emailed; the ball is with the client.</item>
///   <item><see cref="EngagementSetup"/> — the client's form is in; staff are completing the engagement setup.</item>
///   <item><see cref="PendingApproval"/> — routed to the approvers; waiting on their decisions.</item>
///   <item><see cref="ChangesRequested"/> — an approver rejected a round; back with staff for rework.</item>
///   <item><see cref="Approved"/> — every engagement on the request is approved (terminal).</item>
/// </list>
/// The last three are a roll-up of the per-engagement <c>RemsEngagementStatus</c>: a request can carry
/// several engagements (one per entity), so the request-level status summarises them all — see
/// <c>RemsApprovalController.SyncRequestStatusAsync</c>.
/// </summary>
public static class RemsRequestStatuses
{
    /// <summary>A saved, not-yet-submitted request. Visible only to its creator (AC-REMS-004.11).</summary>
    public const string Draft = "draft";

    /// <summary>Submitted to the Admin Pool. The Admin Pool is every request with a status other than <see cref="Draft"/>.</summary>
    public const string Submitted = "submitted";

    /// <summary>The EMS onboarding form has been emailed to the client and is awaiting their submission (REMS WO-112).</summary>
    public const string AwaitingCustomer = "awaiting_customer";

    /// <summary>
    /// The customer completed and submitted their EMS onboarding form (set on public submit, REMS WO-113), so
    /// the request has moved on to the engagement workspace. Labelled "Engagement Setup" — the code is kept
    /// as-is because it is what existing rows hold.
    /// </summary>
    public const string EngagementSetup = "customer_submitted";

    /// <summary>At least one engagement has been routed for approval and no round has come back rejected (REMS WO-114).</summary>
    public const string PendingApproval = "pending_approval";

    /// <summary>An approver rejected a round; the engagement is back with staff for rework and resubmission.</summary>
    public const string ChangesRequested = "changes_requested";

    /// <summary>Every engagement on the request has been fully approved.</summary>
    public const string Approved = "approved";
}
