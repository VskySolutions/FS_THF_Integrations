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

/// <summary>A row in the caller's own approval-task list (pending + historical).</summary>
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
    string EntityName);

/// <summary>A checklist line on an approval task.</summary>
public sealed record RemsChecklistItemView(Guid Id, int DisplayOrder, string Label, bool IsCompleted, DateTime? CompletedOnUtc);

/// <summary>
/// The role-scoped task view (AC-REMS-019.9/10): the task, its checklist, and only the engagement data the
/// approver's role is entitled to. CSE sees the full client + engagement; DepartmentDirector /
/// ManagingShareholder additionally see fee + realization; CommissionRecipient sees the commission splits.
/// Sections not relevant to the role are null.
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
    RemsApprovalEngagementView Engagement);

/// <summary>The role-scoped engagement projection carried by <see cref="RemsApprovalTaskView"/>.</summary>
public sealed record RemsApprovalEngagementView(
    Guid EngagementId,
    Guid RemsId,
    string RemsNumber,
    string EntityName,
    string? Department,
    string? ServiceLine,
    RemsApprovalClientView? Client,
    decimal? FirstYearFeeEstimate,
    decimal? RealizationPercentage,
    RemsUserRef? DepartmentDirector,
    RemsUserRef? EngagementExecutive,
    RemsUserRef? BillingManager,
    IReadOnlyList<RemsCommissionSplitView>? CommissionSplits,
    IReadOnlyList<Guid>? MarketingMethodIds);

/// <summary>The full client projection shown to the CSE approver (AC-REMS-019.9).</summary>
public sealed record RemsApprovalClientView(
    Guid Id,
    string Name,
    string Email,
    string? MobileNumber,
    string? ReferralSource,
    IReadOnlyList<RemsApprovalEntitySummary> Entities);

/// <summary>An entity summary within the CSE client projection.</summary>
public sealed record RemsApprovalEntitySummary(Guid Id, string Name, string? Ein, bool IsMainEntity);

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
