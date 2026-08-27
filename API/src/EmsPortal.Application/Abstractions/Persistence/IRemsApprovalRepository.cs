using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;

namespace EmsPortal.Application.Abstractions.Persistence;

/// <summary>
/// The approvals-inbox query (WO-117): quick search over the REMS number and client name, optional
/// narrowing by the role they act in and their decision state, and server-side paging — an approver
/// accumulates every request they were ever routed, so the list is not bounded.
/// <para>
/// <see cref="Role"/> and <see cref="Status"/> read against the caller's CURRENT task on each request, the
/// one the inbox lists, rather than against any round since superseded.
/// </para>
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
    /// A page of the caller's approvals inbox — ONE task per request, the one on the latest round they were
    /// routed — with round + engagement context and the round's sibling tasks (for the n-of-m progress
    /// badge). Filtered and paged server-side, so the pager reports the filtered total, which is a count of
    /// requests rather than of every round of every one of them.
    /// <para>
    /// The earlier rounds are not lost, only unlisted: the task detail shows them under the round being
    /// decided. Listing each round as a row made a request that had been round three times occupy three
    /// rows, and left the one still wanting an answer indistinguishable from the two that did not.
    /// </para>
    /// </summary>
    Task<(IReadOnlyList<REMSApprovalTask> Items, int Total)> ListTasksByApproverAsync(
        RemsApprovalTaskQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether this user has ever been routed an approval task on any engagement of this request — in any
    /// round, whatever they decided or whether they decided at all.
    /// <para>
    /// It answers "may this person read the request they were asked to sign off on". Being asked is what
    /// grants it, so it deliberately outlives the asking: an approver whose round was declined by somebody
    /// else still needs the request open in front of them to read WHY, and a superseded task is exactly the
    /// case where that matters most.
    /// </para>
    /// </summary>
    Task<bool> IsApproverOnRequestAsync(Guid remsId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The id of the caller's CURRENT task on a request — their own task on the latest round they were
    /// routed — or null if they were never an approver on it.
    /// <para>
    /// Deliberately the same task the inbox row for that request opens: the newest of THEIR OWN tasks
    /// (highest round number, id as the tie-break), not the request's newest round. An approver dropped
    /// from a later round — a commission recipient taken off the split — still holds the last round they
    /// were actually on, and that is the one they are sent to.
    /// </para>
    /// <para>
    /// Backs the approver deep-link: a REMS notification carries the REQUEST id, so an approver following
    /// one landed on the request rather than on the task it was asking them to decide.
    /// </para>
    /// </summary>
    Task<Guid?> GetCurrentTaskIdOnRequestAsync(Guid remsId, Guid userId, CancellationToken cancellationToken = default);

    Task AddRoundAsync(REMSApprovalRound round, CancellationToken cancellationToken = default);

    void UpdateRound(REMSApprovalRound round);

    Task AddTaskAsync(REMSApprovalTask task, CancellationToken cancellationToken = default);

    void UpdateTask(REMSApprovalTask task);

    Task AddChecklistItemAsync(REMSApprovalChecklistItem item, CancellationToken cancellationToken = default);

    void UpdateChecklistItem(REMSApprovalChecklistItem item);
}
