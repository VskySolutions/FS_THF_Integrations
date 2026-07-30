using EmsPortal.Domain.Enums;

namespace EmsPortal.Api.Models.Rems;

// ---------------------------------------------------------------------------------------------------
// WO-114 — REMS approval workflow (Part C): the live suggested approver list, the caller's own tasks,
// the role-scoped task view, and the checklist/approve/reject payloads.
// ---------------------------------------------------------------------------------------------------

/// <summary>One suggested approver on the live list (AC-REMS-018): the user and the role they would act in.</summary>
public sealed record RemsApproverSuggestion(RemsUserRef User, string Role);

/// <summary>The live suggested approver list for an engagement (updates until the round is sent).</summary>
public sealed record RemsApproverList(Guid EngagementId, string EngagementStatus, IReadOnlyList<RemsApproverSuggestion> Approvers);

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

    /// <summary>The ordered checklist labels for an approver role.</summary>
    public static IReadOnlyList<string> For(RemsApproverRole role) => role switch
    {
        RemsApproverRole.CSE => Cse,
        RemsApproverRole.DepartmentDirector => DepartmentDirector,
        RemsApproverRole.ManagingShareholder => ManagingShareholder,
        RemsApproverRole.CommissionRecipient => CommissionRecipient,
        _ => Array.Empty<string>(),
    };
}
