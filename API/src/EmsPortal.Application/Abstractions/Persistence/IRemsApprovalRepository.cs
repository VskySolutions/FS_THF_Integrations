using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;

namespace EmsPortal.Application.Abstractions.Persistence;

/// <summary>
/// The approver's own task-list query (WO-117): quick search over the REMS number, client and entity name,
/// optional narrowing by the role they act in and their decision state, and server-side paging — an
/// approver accumulates every historical task they were ever routed, so the list is not bounded.
/// </summary>
public sealed record RemsApprovalTaskQuery(
    Guid ApproverId,
    string? Search,
    RemsApproverRole? Role,
    RemsApprovalTaskStatus? Status,
    int Page,
    int Limit);

/// <summary>
/// Data access for the REMS approval chain (WO-110): immutable rounds, per-approver tasks and their
/// checklist items. A resubmission creates a new round (with a higher round number) and fresh tasks.
/// </summary>
public interface IRemsApprovalRepository
{
    /// <summary>The round with its tasks and their checklist items loaded.</summary>
    Task<REMSApprovalRound?> GetRoundByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>All rounds (history) for an engagement, newest round first, with tasks/checklists loaded.</summary>
    Task<IReadOnlyList<REMSApprovalRound>> GetRoundsByEngagementAsync(Guid engagementId, CancellationToken cancellationToken = default);

    /// <summary>The next 1-based round number for an engagement (max existing round number + 1).</summary>
    Task<int> GetNextRoundNumberAsync(Guid engagementId, CancellationToken cancellationToken = default);

    /// <summary>The task with its checklist items loaded.</summary>
    Task<REMSApprovalTask?> GetTaskByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The task with its full decision context (WO-114): its checklist, its round (and all sibling tasks in
    /// the round), and the round's engagement (with commission splits and marketing methods) resolved through
    /// its entity → client (→ entities) → owning REMS request. Backs the role-scoped task view and the
    /// approve/reject lifecycle (sibling-task completion + "everyone involved" notification set).
    /// </summary>
    Task<REMSApprovalTask?> GetTaskWithContextAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// A page of the caller's own approval tasks (pending and historical), newest round first, with round +
    /// engagement context and the round's sibling tasks (for the n-of-m progress badge). Filtered and paged
    /// server-side, so the pager reports the filtered total rather than the page in hand.
    /// </summary>
    Task<(IReadOnlyList<REMSApprovalTask> Items, int Total)> ListTasksByApproverAsync(
        RemsApprovalTaskQuery query, CancellationToken cancellationToken = default);

    Task AddRoundAsync(REMSApprovalRound round, CancellationToken cancellationToken = default);

    void UpdateRound(REMSApprovalRound round);

    Task AddTaskAsync(REMSApprovalTask task, CancellationToken cancellationToken = default);

    void UpdateTask(REMSApprovalTask task);

    Task AddChecklistItemAsync(REMSApprovalChecklistItem item, CancellationToken cancellationToken = default);

    void UpdateChecklistItem(REMSApprovalChecklistItem item);
}
