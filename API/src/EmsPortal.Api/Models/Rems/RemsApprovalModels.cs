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
/// approvers — the CSE and every commission recipient — plus anyone added on the Approval tab.
/// <see cref="SelectedApproverIds"/> is just the added ones, so the picker shows only what a user chose
/// rather than the people who are approvers regardless.
/// </summary>
public sealed record RemsApproverList(
    Guid EngagementId,
    string EngagementStatus,
    IReadOnlyList<RemsApproverSuggestion> Approvers,
    IReadOnlyList<Guid> SelectedApproverIds);

/// <summary>
/// A user selectable as an extra approver: someone holding the Approver role in the active tenant. The
/// job title travels with them so the picker can read "Full Name — Job Title".
/// </summary>
public sealed record RemsApproverOption(Guid UserId, string Name, string? JobTitle, string? Email);

/// <summary>
/// Replaces the engagement's ADDED approvers with exactly these users (AC-REMS-018). An empty list removes
/// the additions; the automatic approvers are unaffected either way.
/// </summary>
public sealed class SetRemsApproversRequest
{
    public List<Guid> UserIds { get; set; } = new();
}

/// <summary>
/// A row in the caller's own approval-task list (pending + historical). The three counts describe the
/// whole ROUND, not just the caller's task, so the inbox can show how far along an engagement is —
/// "1/4 approved" answers "is this waiting on me alone, or on five other people too?".
/// Still awaiting = <c>ApproverCount - ApprovedCount - RejectedCount</c>; a rejection ends the round, so
/// the remaining tasks stay pending and never decide.
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
    string ClientName,
    string EntityName,
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
/// Department Director and Managing Shareholder (AC-REMS-019.10). For every other role they arrive null
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
    RemsApprovalRoundView Round);

/// <summary>
/// The originating REMS request as an approver sees it: the intake fields the partner raised, who is on it,
/// and the attachments that came with it.
/// </summary>
public sealed record RemsApprovalRequestView(
    Guid RemsId,
    string RemsNumber,
    string Title,
    string? Description,
    string RequestedClientName,
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
    string? ServiceLine,
    RemsApprovalClientView Client,
    RemsApprovalEntityView Entity,
    RemsUserRef? DepartmentDirector,
    RemsUserRef? EngagementExecutive,
    RemsUserRef? BillingManager,
    decimal? FirstYearFeeEstimate,
    decimal? RealizationPercentage,
    bool FinancialsRestricted,
    RemsApprovalAuditDetailView? Audit,
    RemsGovernmentDetailView? Government,
    RemsApprovalTaxDetailView? Tax,
    IReadOnlyList<RemsApprovalOptionRef> MarketingMethods,
    IReadOnlyList<RemsCommissionSplitView> CommissionSplits);

/// <summary>The client on the engagement, including the billing block the workspace's client card carries.</summary>
public sealed record RemsApprovalClientView(
    Guid Id,
    string Name,
    string Email,
    string? MobileNumber,
    string? ReferralSource,
    string? BillingContactName,
    string? BillingEmail,
    RemsAddressView? BillingAddress,
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
    string? Url);

/// <summary>Tax detail with the due-date schedule deserialized and the form checklist resolved to labels.</summary>
public sealed record RemsApprovalTaxDetailView(
    Guid Id,
    DateOnly? FiscalYearEnd,
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
/// ManagingShareholder = 3, CommissionRecipient = 2. Defined as constants here (they could become
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

    private static readonly IReadOnlyList<string> ManagingShareholder = new[]
    {
        "Firm risk and independence reviewed",
        "Fee and realization approved",
        "Final engagement acceptance",
    };

    private static readonly IReadOnlyList<string> CommissionRecipient = new[]
    {
        "Commission split reviewed",
        "Commission allocation accepted",
    };

    // A hand-picked approver with no other standing on the engagement: a general review, since nothing
    // narrower can be assumed about why they were added.
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
        RemsApproverRole.ManagingShareholder => ManagingShareholder,
        RemsApproverRole.CommissionRecipient => CommissionRecipient,
        RemsApproverRole.Approver => Approver,
        _ => Array.Empty<string>(),
    };
}
