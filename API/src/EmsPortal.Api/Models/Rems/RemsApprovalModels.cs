using EmsPortal.Domain.Enums;

namespace EmsPortal.Api.Models.Rems;

// ---------------------------------------------------------------------------------------------------
// WO-114 — REMS approval workflow (Part C): the live suggested approver list, the caller's own tasks,
// the role-scoped task view, and the checklist/approve/reject payloads.
// ---------------------------------------------------------------------------------------------------

/// <summary>One suggested approver on the live list (AC-REMS-018): the user and the role they would act in.</summary>
public sealed record RemsApproverSuggestion(RemsUserRef User, string Role);

/// <summary>
/// The full approver list an engagement will route to (updates until the round is sent): the automatic
/// approvers — the firm's shareholders, the Department Director, the CSE and every commission recipient —
/// plus anyone added on the Approval tab. Ordered for reading, shareholders first.
/// <see cref="SelectedApproverIds"/> is just the added ones, so the picker shows only what a user chose
/// rather than the people who are approvers regardless — and cannot be used to take one of them off.
/// </summary>
public sealed record RemsApproverList(
    Guid EngagementId,
    string EngagementStatus,
    IReadOnlyList<RemsApproverSuggestion> Approvers,
    IReadOnlyList<Guid> SelectedApproverIds);

/// <summary>
/// A user selectable as an extra approver: any active user in the tenant (there is no Approver role).
/// <para>
/// <paramref name="Roles"/> is every role they hold in this tenant, which is what the picker shows beside
/// the name: choosing who else should sign off is a question about what someone IS to the firm, and a list
/// of bare names cannot answer it. The email travels with them too, as the tiebreak when two people share
/// a name and a role.
/// </para>
/// </summary>
public sealed record RemsApproverOption(
    Guid UserId, string Name, string? Email, IReadOnlyList<string> Roles);

/// <summary>
/// Replaces the engagement's ADDED approvers with exactly these users (AC-REMS-018). An empty list removes
/// the additions; the automatic approvers — shareholders among them — are unaffected either way, which is
/// what makes them impossible to remove from here.
/// </summary>
public sealed class SetRemsApproversRequest
{
    public List<Guid> UserIds { get; set; } = new();
}

/// <summary>
/// One REQUEST in the caller's approvals inbox, carried by their task on its latest round — so
/// <see cref="Role"/>, <see cref="Status"/> and <see cref="RoundNumber"/> say what they are to it now, and
/// the rounds before this one are read on the task detail rather than listed here as rows of their own.
/// <para>
/// The three counts describe the whole ROUND, not just the caller's task, so the inbox can show how far
/// along an engagement is — "1/4 approved" answers "is this waiting on me alone, or on five other people
/// too?". Still awaiting = <c>ApproverCount - ApprovedCount - RejectedCount</c>; a rejection ends the
/// round, so the remaining tasks stay pending and never decide.
/// </para>
/// </summary>
public sealed record RemsApprovalTaskRow(
    Guid TaskId,
    Guid RoundId,
    int RoundNumber,
    string Role,
    string Status,
    DateTime SentOnUtc,
    DateTime? DecidedOnUtc,
    string RoundStatus,
    Guid EngagementId,
    Guid RemsId,
    string RemsNumber,
    /// <summary>The client's name as it reads — the suffix in front of the name they gave on intake.</summary>
    string ClientName,
    /// <summary>The two halves of that name, so the Client column can draw the particle in bold.</summary>
    string ClientNameWithoutSuffix,
    string? ClientNameSuffix,
    /// <summary>
    /// The request's Client Service Executive. An approver deciding on a round needs to know who to ask
    /// about it, and the CSE is that person — so the inbox shows them by default rather than making the
    /// approver open the request to find out.
    /// </summary>
    RemsUserRef? Cse,
    // No EntityName: an approval is about a request and its single engagement now, so the entity's name
    // only ever repeated the client's.
    int ApprovedCount,
    int RejectedCount,
    int ApproverCount,
    // The TASK's own audit trail — the row is keyed on it — offered as hidden-by-default columns.
    string? CreatedBy,
    DateTime CreatedOnUtc,
    string? UpdatedBy,
    DateTime UpdatedOnUtc);

/// <summary>A checklist line on an approval task.</summary>
public sealed record RemsChecklistItemView(Guid Id, int DisplayOrder, string Label, bool IsCompleted, DateTime? CompletedOnUtc);

/// <summary>
/// The approval-task review packet: the task, its checklist, and the complete case an approver decides on —
/// the originating REMS request, the client, the entity under review, the engagement setup with its
/// conditional audit/government/tax detail, the marketing tags, the commission splits, and the round's
/// other decisions. Deliberately the SAME material as the staff engagement workspace's four tabs, since an
/// approver is being asked to sign off on exactly what staff filled in.
/// <para>
/// One thing stays role-scoped: the first-year fee estimate and % realization are reserved to the
/// Department Director (AC-REMS-019.10). For every other role they arrive null
/// with <see cref="RemsApprovalEngagementView.FinancialsRestricted"/> set, so the UI can say the figures
/// are withheld rather than render them as blank.
/// </para>
/// </summary>
public sealed record RemsApprovalTaskView(
    Guid TaskId,
    Guid RoundId,
    int RoundNumber,
    string Role,
    string Status,
    DateTime? DecidedOnUtc,
    string? RejectionReason,
    bool CanDecide,
    IReadOnlyList<RemsChecklistItemView> Checklist,
    RemsApprovalRequestView Request,
    RemsApprovalEngagementView Engagement,
    RemsApprovalRoundView Round,
    /// <summary>The TASK's own provenance — the packet is keyed on it — as every detail page ends with.</summary>
    RecordAudit Audit);

/// <summary>
/// The originating REMS request as an approver sees it: the intake fields the partner raised, who is on it,
/// and the attachments that came with it.
/// </summary>
public sealed record RemsApprovalRequestView(
    Guid RemsId,
    string RemsNumber,
    string? Description,
    /// <summary>The name the request was raised under, WITHOUT the particle.</summary>
    string RequestedClientName,
    /// <summary>The particle, so the packet can draw it in front of that name and in bold.</summary>
    string? ClientNameSuffix,
    string Type,
    string Status,
    string? CustomerEmail,
    string? CustomerMobileNumber,
    string? IndustryGroup,
    string EmsFormState,
    string? ClientSubmissionState,
    RemsUserRef? AssignedAdmin,
    RemsUserRef? Cse,
    string? RequestedBy,
    DateTime CreatedOnUtc,
    IReadOnlyList<RemsFileRef> Files);

/// <summary>
/// The engagement under review, mirroring the workspace's Setup / Marketing / Commission tabs.
/// <c>FinancialsRestricted</c> is true when the fee estimate and realization were withheld from this
/// approver's role rather than simply never filled in — the difference the UI needs to say "reserved"
/// instead of "—".
/// </summary>
public sealed record RemsApprovalEngagementView(
    Guid EngagementId,
    string Status,
    string? Department,
    string? SubServiceLine,
    string? SubIndustry,
    RemsApprovalClientView Client,
    RemsApprovalEntityView Entity,
    RemsUserRef? DepartmentDirector,
    RemsUserRef? EngagementExecutive,
    RemsUserRef? BillingManager,
    decimal? FirstYearFeeEstimate,
    /// <summary>Assurance prices the engagement rather than its first year; only one of the two is ever set.</summary>
    decimal? EngagementFee,
    decimal? RealizationPercentage,
    bool FinancialsRestricted,
    /// <summary>
    /// How often the client is billed (option-set <c>REMS.BillingPeriod</c> code), and how that billing
    /// actually runs. Asked of CAS engagements and no others — a recurring arrangement is part of what the
    /// approvers are being asked to accept, and the packet did not carry either answer.
    /// </summary>
    string? BillingPeriod,
    string? BillingProcessDescription,
    RemsApprovalAuditDetailView? Audit,
    RemsGovernmentDetailView? Government,
    RemsApprovalTaxDetailView? Tax,
    IReadOnlyList<RemsApprovalOptionRef> MarketingMethods,
    IReadOnlyList<RemsCommissionSplitView> CommissionSplits);

/// <summary>
/// One approval round as history. Rounds are immutable and numbered from 1: a resubmission creates a new
/// one rather than resetting the last, so the list is the whole record of what the approvers did.
/// </summary>
/// <summary>
/// A pointer to one approval task and nothing else — what the approver deep-link needs to navigate. It
/// carries no round, no engagement and no client: the caller is about to open the task itself, which
/// returns the whole packet under its own permission rule.
/// </summary>
public sealed record RemsApprovalTaskRef(Guid TaskId);

public sealed record RemsApprovalRoundHistory(
    Guid RoundId,
    int RoundNumber,
    string Status,
    DateTime SentOnUtc,
    string? SentBy,
    DateTime? CompletedOnUtc,
    // What it would have taken to close this round, and how close it got. One decline closes a round
    // now, so these agree on anything that failed today — but a round closed under the old two-decline
    // threshold still carries the count it actually took, against a threshold recomputed as one.
    int DeclineThreshold,
    int DeclineCount,
    IReadOnlyList<RemsApprovalRoundDecision> Decisions);

/// <summary>One approver's decision within a round, with the checklist they worked through.</summary>
public sealed record RemsApprovalRoundDecision(
    Guid TaskId,
    string Approver,
    string Role,
    string Status,
    DateTime? DecidedOnUtc,
    /// <summary>Their own reason for declining. The round-level reason cannot hold several.</summary>
    string? Reason,
    int ChecklistCompleted,
    int ChecklistTotal);

/// <summary>The client on the engagement, including the billing block the workspace's client card carries.</summary>
public sealed record RemsApprovalClientView(
    Guid Id,
    string Name,
    string Email,
    string? MobileNumber,
    string? ReferralSource,
    string? BillingContactName,
    string? BillingEmail,
    // The billing ADDRESS moved onto the entity, so it arrives with that entity's addresses rather than
    // separately here.
    IReadOnlyList<RemsApprovalEntitySummary> Entities);

/// <summary>A sibling entity of the same client, listed for context.</summary>
public sealed record RemsApprovalEntitySummary(Guid Id, string Name, string? Ein, bool IsMainEntity);

/// <summary>The entity this engagement belongs to, with the addresses and contacts from the submitted form.</summary>
public sealed record RemsApprovalEntityView(
    Guid Id,
    string Name,
    string? Ein,
    bool IsMainEntity,
    IReadOnlyList<RemsEntityAddressView> Addresses,
    IReadOnlyList<RemsEntityContactView> Contacts);

/// <summary>
/// An option-set item resolved to its LABEL (and group, for marketing). Approver roles do not carry
/// <c>optionSets.read</c>, so ids alone would be unreadable on this screen — the server resolves them.
/// </summary>
public sealed record RemsApprovalOptionRef(Guid Id, string Label, string? Group);

/// <summary>Audit detail with the signed client-acceptance form resolved to something openable.</summary>
public sealed record RemsApprovalAuditDetailView(
    Guid Id,
    Guid? ClientAcceptanceFormMediaId,
    string? FileName,
    string? Url,
    DateOnly? ClientFiscalYearEnd,
    bool? AdminFeesApply,
    decimal? AdminFeesAmount);

/// <summary>Tax detail with the due-date schedule deserialized and the form checklist resolved to labels.</summary>
public sealed record RemsApprovalTaxDetailView(
    Guid Id,
    DateOnly? FiscalYearEnd,
    /// <summary>Derived from the fiscal year end, then whatever was typed over it. Never recomputed here.</summary>
    RemsTaxDueDateSet? DueDates,
    IReadOnlyList<RemsApprovalOptionRef> TaxForms);

/// <summary>
/// The approval round this task belongs to, with every approver's decision — the approver-side equivalent
/// of the workspace's Approval tab, so a reviewer can see who else is on the round and where it stands.
/// <see cref="Decisions"/> arrives in round order: those who have decided first, oldest decision leading,
/// then everyone still to decide.
/// </summary>
public sealed record RemsApprovalRoundView(
    Guid Id,
    int RoundNumber,
    string Status,
    DateTime SentOnUtc,
    RemsUserRef? SentBy,
    DateTime? CompletedOnUtc,
    string? RejectionReason,
    IReadOnlyList<RemsApprovalDecisionView> Decisions);

/// <summary>One approver's standing on the round. <see cref="IsYou"/> marks the caller's own task.</summary>
public sealed record RemsApprovalDecisionView(
    Guid TaskId,
    RemsUserRef Approver,
    string Role,
    string Status,
    DateTime? DecidedOnUtc,
    string? RejectionReason,
    bool IsYou);

/// <summary>Check / uncheck a checklist item on the caller's own task.</summary>
public sealed class SetChecklistItemRequest
{
    public bool IsCompleted { get; set; }
}

/// <summary>Reject the caller's own task with a required reason (AC-REMS-020.1).</summary>
public sealed class RejectApprovalTaskRequest
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// The per-role approval checklist labels (AC-REMS-019.4/5/6): CSE = 2, DepartmentDirector = 3,
/// CommissionRecipient = 2. Defined as constants here (they could become
/// option-set-driven later). Rows are created in order as <c>REMSApprovalChecklistItem</c>s.
/// </summary>
public static class RemsApprovalChecklistCatalog
{
    private static readonly IReadOnlyList<string> Cse = new[]
    {
        "Client information reviewed and accurate",
        "Engagement scope and service line confirmed",
    };

    private static readonly IReadOnlyList<string> DepartmentDirector = new[]
    {
        "First-year fee estimate reviewed",
        "Realization percentage acceptable",
        "Engagement team and department placement appropriate",
    };

    private static readonly IReadOnlyList<string> CommissionRecipient = new[]
    {
        "Commission split reviewed",
        "Commission allocation accepted",
    };

    // An approver with no other standing on the engagement: a general review, since nothing narrower can
    // be assumed about why they are looking at it. Shared with Shareholder — being asked about every
    // engagement says nothing about what to ask them, and a firm-wide checklist would have to be general
    // in exactly this way.
    private static readonly IReadOnlyList<string> Approver = new[]
    {
        "Engagement details reviewed",
        "Engagement accepted",
    };

    /// <summary>The ordered checklist labels for an approver role.</summary>
    public static IReadOnlyList<string> For(RemsApproverRole role) => role switch
    {
        RemsApproverRole.CSE => Cse,
        RemsApproverRole.DepartmentDirector => DepartmentDirector,
        RemsApproverRole.CommissionRecipient => CommissionRecipient,
        RemsApproverRole.Shareholder or RemsApproverRole.Approver => Approver,
        _ => Array.Empty<string>(),
    };
}
