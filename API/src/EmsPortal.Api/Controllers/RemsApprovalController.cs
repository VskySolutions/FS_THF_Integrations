using EmsPortal.Api.Models.Rems;
using EmsPortal.Api.Security;
using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Application.Abstractions.UniversalFeatures;
using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;
using EmsPortal.Shared.Contracts;
using EmsPortal.Shared.Security;
using Microsoft.AspNetCore.Mvc;

namespace EmsPortal.Api.Controllers;

/// <summary>
/// REMS approval workflow backend (WO-114 Part C). Staff route an engagement for approval and manage
/// resubmission (<see cref="Permissions.RemsApprovalsSend"/>); approvers act ONLY on their own tasks
/// (<see cref="Permissions.RemsApprovalsAct"/> + a record-level <c>ApproverId == caller</c> check). A task
/// or engagement is never revealed to a user merely for holding the Approver role: a task that is not the
/// caller's own is a 404. Tenant isolation is ambient.
/// </summary>
[ApiController]
[Route("api/rems")]
[Produces("application/json")]
[Tags("REMS Approval")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status500InternalServerError)]
public sealed class RemsApprovalController : ControllerBase
{
    private const string CodeSetupIncomplete = "REMS_SETUP_INCOMPLETE";
    private const string CodeMarketingRequired = "REMS_MARKETING_REQUIRED";
    private const string CodeCafRequired = "REMS_CAF_REQUIRED";
    private const string CodeGovDetailRequired = "REMS_GOV_DETAIL_REQUIRED";
    private const string CodeNoApprovers = "REMS_NO_APPROVERS";
    private const string CodeNotSendable = "REMS_NOT_SENDABLE";
    private const string CodeNotRejected = "REMS_NOT_REJECTED";
    private const string CodeTaskDecided = "REMS_TASK_ALREADY_DECIDED";
    private const string CodeRoundClosed = "REMS_ROUND_CLOSED";
    private const string CodeChecklistIncomplete = "REMS_CHECKLIST_INCOMPLETE";

    private readonly IRemsEngagementRepository _engagements;
    private readonly IRemsApprovalRepository _approvals;
    private readonly IRemsSettingsRepository _settings;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IActivityEventWriter _activity;
    private readonly INotificationDispatcher _notifications;

    public RemsApprovalController(
        IRemsEngagementRepository engagements,
        IRemsApprovalRepository approvals,
        IRemsSettingsRepository settings,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IActivityEventWriter activity,
        INotificationDispatcher notifications)
    {
        _engagements = engagements;
        _approvals = approvals;
        _settings = settings;
        _users = users;
        _unitOfWork = unitOfWork;
        _activity = activity;
        _notifications = notifications;
    }

    // -------------------- Suggested approvers (live) --------------------

    /// <summary>
    /// The live suggested approver list (AC-REMS-018): CSE, the mapped department director (unless
    /// unassigned), the managing shareholder, and every commission recipient — deduped by (user, role). The
    /// list updates until the round is sent.
    /// </summary>
    [HttpGet("engagements/{id:guid}/approvers")]
    [RequirePermission(Permissions.RemsEngagementsManage)]
    [ProducesResponseType<ApiResponse<RemsApproverList>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Approvers(Guid id, CancellationToken cancellationToken)
    {
        var engagement = await _engagements.GetWithContextAsync(id, cancellationToken);
        if (engagement is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS engagement not found."));
        }

        var approvers = await BuildApproverListAsync(engagement, cancellationToken);
        var list = await ToApproverListAsync(engagement, approvers, cancellationToken);
        return Ok(ApiResponseFactory.Success(list, "REMS suggested approvers retrieved."));
    }

    // -------------------- Send / resubmit --------------------

    /// <summary>
    /// Route the engagement for approval (AC-REMS-018/019). Pre-requisites: at least one marketing tag; an
    /// audit engagement has its signed CAF; a government audit has a contract number and the Florida 1% flag.
    /// Transactionally creates the approval round, a per-approver task with its role checklist, locks the
    /// approver list, sets the engagement to PendingApproval, notifies every approver, and logs the send.
    /// </summary>
    [HttpPost("engagements/{id:guid}/approval/send")]
    [RequirePermission(Permissions.RemsApprovalsSend)]
    [ProducesResponseType<ApiResponse<RemsApproverList>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Send(Guid id, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } me)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        var engagement = await _engagements.GetWithContextAsync(id, cancellationToken);
        if (engagement is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS engagement not found."));
        }
        if (engagement.Status != RemsEngagementStatus.Draft)
        {
            return ConflictResult(CodeNotSendable, "Only a draft engagement can be sent for approval; a rejected one must be resubmitted.");
        }

        if (await ValidateApprovalPrerequisitesAsync(engagement, cancellationToken) is { } prereqError)
        {
            return prereqError;
        }

        var approvers = await BuildApproverListAsync(engagement, cancellationToken);
        if (approvers.Count == 0)
        {
            return ConflictResult(CodeNoApprovers, "There are no approvers for this engagement; assign a CSE, director, managing shareholder or commission recipient first.");
        }

        await CreateRoundAsync(engagement, approvers, me, isResubmission: false, cancellationToken);

        var list = await ToApproverListAsync(engagement, approvers, cancellationToken);
        return Ok(ApiResponseFactory.Success(list, "REMS engagement sent for approval."));
    }

    /// <summary>
    /// Resubmit a rejected engagement (AC-REMS-020): allowed only after a rejected round. Regenerates the
    /// (live) approver list, creates a NEW round with fresh pending tasks and blank checklists, notifies every
    /// approver anew, and logs the resubmission distinctly from an original send.
    /// </summary>
    [HttpPost("engagements/{id:guid}/approval/resubmit")]
    [RequirePermission(Permissions.RemsApprovalsSend)]
    [ProducesResponseType<ApiResponse<RemsApproverList>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Resubmit(Guid id, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } me)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        var engagement = await _engagements.GetWithContextAsync(id, cancellationToken);
        if (engagement is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS engagement not found."));
        }
        if (engagement.Status != RemsEngagementStatus.Rejected)
        {
            return ConflictResult(CodeNotRejected, "Only a rejected engagement can be resubmitted.");
        }

        if (await ValidateApprovalPrerequisitesAsync(engagement, cancellationToken) is { } prereqError)
        {
            return prereqError;
        }

        var approvers = await BuildApproverListAsync(engagement, cancellationToken);
        if (approvers.Count == 0)
        {
            return ConflictResult(CodeNoApprovers, "There are no approvers for this engagement.");
        }

        await CreateRoundAsync(engagement, approvers, me, isResubmission: true, cancellationToken);

        var list = await ToApproverListAsync(engagement, approvers, cancellationToken);
        return Ok(ApiResponseFactory.Success(list, "REMS engagement resubmitted for approval."));
    }

    // -------------------- Approver's own tasks --------------------

    /// <summary>The caller's own approval tasks (pending and historical), newest round first (AC-REMS-019).</summary>
    [HttpGet("approval-tasks")]
    [RequirePermission(Permissions.RemsApprovalsAct)]
    [ProducesResponseType<ApiResponse<IEnumerable<RemsApprovalTaskRow>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> MyTasks(CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } me)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        var tasks = await _approvals.ListTasksByApproverAsync(me, cancellationToken);
        var rows = tasks.Select(t =>
        {
            var round = t.Round!;
            var engagement = round.Engagement!;
            var client = engagement.Entity!.Client!;
            var rems = client.Rems!;
            return new RemsApprovalTaskRow(
                t.Id, round.Id, round.RoundNumber, t.ApproverRole.ToString(), t.Status.ToString(),
                round.SentOnUtc, t.DecidedOnUtc, round.Status.ToString(),
                engagement.Id, rems.Id, rems.REMSNumber, client.Name, engagement.Entity!.Name);
        });
        return Ok(ApiResponseFactory.Success(rows, "REMS approval tasks retrieved."));
    }

    /// <summary>
    /// The role-scoped view of the caller's own task (AC-REMS-019.9/10): CSE sees the full client +
    /// engagement; DepartmentDirector / ManagingShareholder additionally see the fee estimate and
    /// realization; CommissionRecipient sees the commission splits. Includes the checklist. Not the caller's
    /// own task =&gt; 404.
    /// </summary>
    [HttpGet("approval-tasks/{taskId:guid}")]
    [RequirePermission(Permissions.RemsApprovalsAct)]
    [ProducesResponseType<ApiResponse<RemsApprovalTaskView>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTask(Guid taskId, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } me)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        var task = await _approvals.GetTaskWithContextAsync(taskId, cancellationToken);
        // Record-level: an approver may read ONLY their own task; anything else is a 404 (never revealed).
        if (task is null || task.ApproverId != me)
        {
            return NotFound(ApiResponseFactory.NotFound("Approval task not found."));
        }

        var view = await BuildTaskViewAsync(task, cancellationToken);
        return Ok(ApiResponseFactory.Success(view, "REMS approval task retrieved."));
    }

    /// <summary>Check / uncheck a checklist item on the caller's own task (AC-REMS-019).</summary>
    [HttpPut("approval-tasks/{taskId:guid}/checklist/{itemId:guid}")]
    [RequirePermission(Permissions.RemsApprovalsAct)]
    [ProducesResponseType<ApiResponse<RemsChecklistItemView>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetChecklistItem(
        Guid taskId, Guid itemId, [FromBody] SetChecklistItemRequest request, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } me)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        var task = await _approvals.GetTaskByIdAsync(taskId, cancellationToken);
        if (task is null || task.ApproverId != me)
        {
            return NotFound(ApiResponseFactory.NotFound("Approval task not found."));
        }
        if (task.Status != RemsApprovalTaskStatus.Pending)
        {
            return ConflictResult(CodeTaskDecided, "This task has already been decided.");
        }

        var item = task.ChecklistItems.FirstOrDefault(i => i.Id == itemId && !i.Deleted);
        if (item is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Checklist item not found."));
        }

        item.IsCompleted = request.IsCompleted;
        item.CompletedOnUtc = request.IsCompleted ? DateTime.UtcNow : null;
        _approvals.UpdateChecklistItem(item);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var view = new RemsChecklistItemView(item.Id, item.DisplayOrder, item.Label, item.IsCompleted, item.CompletedOnUtc);
        return Ok(ApiResponseFactory.Success(view, "Checklist item updated."));
    }

    /// <summary>
    /// Approve the caller's own task (AC-REMS-019). Re-verifies every checklist item is completed server-side.
    /// When it is the last pending task, the round and engagement become Approved and a single full-approval
    /// notification goes to everyone involved.
    /// </summary>
    [HttpPost("approval-tasks/{taskId:guid}/approve")]
    [RequirePermission(Permissions.RemsApprovalsAct)]
    [ProducesResponseType<ApiResponse<RemsApprovalTaskView>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Approve(Guid taskId, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } me)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        var task = await _approvals.GetTaskWithContextAsync(taskId, cancellationToken);
        if (task is null || task.ApproverId != me)
        {
            return NotFound(ApiResponseFactory.NotFound("Approval task not found."));
        }

        var round = task.Round!;
        if (task.Status != RemsApprovalTaskStatus.Pending)
        {
            return ConflictResult(CodeTaskDecided, "This task has already been decided.");
        }
        if (round.Status != RemsApprovalRoundStatus.Pending)
        {
            return ConflictResult(CodeRoundClosed, "This approval round is already closed.");
        }

        // Re-verify server-side that every checklist item is completed (AC-REMS-019.7/8).
        if (task.ChecklistItems.Any(i => !i.Deleted && !i.IsCompleted))
        {
            return ConflictResult(CodeChecklistIncomplete, "All checklist items must be completed before approving.");
        }

        var now = DateTime.UtcNow;
        var engagement = round.Engagement!;
        var rems = engagement.Entity!.Client!.Rems!;

        task.Status = RemsApprovalTaskStatus.Approved;
        task.DecidedOnUtc = now;
        _approvals.UpdateTask(task);
        await _activity.WriteAsync(new CreateActivityEventDto(EntityType.Rems, rems.Id, ActivityEventTypes.RemsApproved, null, task.ApproverRole.ToString()), cancellationToken);

        // When this was the last pending task, the whole round is approved: flip the round + engagement and
        // raise the full-approval notification EXACTLY ONCE to everyone involved (AC-REMS-019.1/12).
        var fullyApproved = round.Tasks.All(t => t.Status == RemsApprovalTaskStatus.Approved);
        if (fullyApproved)
        {
            round.Status = RemsApprovalRoundStatus.Approved;
            round.CompletedOnUtc = now;
            _approvals.UpdateRound(round);

            engagement.Status = RemsEngagementStatus.Approved;
            _engagements.Update(engagement);

            var involved = new HashSet<Guid>(round.Tasks.Select(t => t.ApproverId)) { round.SentByUserId };
            if (rems.CSEId is { } cse)
            {
                involved.Add(cse);
            }
            // Final approval is the outcome the requester has been waiting for since they submitted.
            // (Rejections stay internal — they are a rework loop between staff, not a status for the requester.)
            if (rems.CreatedById is { } requester)
            {
                involved.Add(requester);
            }
            foreach (var userId in involved)
            {
                await _notifications.DispatchAsync(new CreateNotificationDto(
                    userId, NotificationType.RemsEngagementApproved,
                    "A REMS engagement was fully approved",
                    $"{rems.REMSNumber} — {rems.Title}", EntityType.Rems, rems.Id), cancellationToken);
            }
            await _activity.WriteAsync(new CreateActivityEventDto(EntityType.Rems, rems.Id, ActivityEventTypes.RemsFullyApproved), cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var view = await BuildTaskViewAsync(task, cancellationToken);
        return Ok(ApiResponseFactory.Success(view, fullyApproved ? "Task approved; engagement fully approved." : "Task approved."));
    }

    /// <summary>
    /// Reject the caller's own task with a required reason (AC-REMS-020). Ends the round and sets the
    /// engagement to Rejected; the reason is retained (visible to CSE + Admin) until resubmission, and the
    /// sender and CSE are notified.
    /// </summary>
    [HttpPost("approval-tasks/{taskId:guid}/reject")]
    [RequirePermission(Permissions.RemsApprovalsAct)]
    [ProducesResponseType<ApiResponse<RemsApprovalTaskView>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(Guid taskId, [FromBody] RejectApprovalTaskRequest request, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } me)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        var task = await _approvals.GetTaskWithContextAsync(taskId, cancellationToken);
        if (task is null || task.ApproverId != me)
        {
            return NotFound(ApiResponseFactory.NotFound("Approval task not found."));
        }

        var round = task.Round!;
        if (task.Status != RemsApprovalTaskStatus.Pending)
        {
            return ConflictResult(CodeTaskDecided, "This task has already been decided.");
        }
        if (round.Status != RemsApprovalRoundStatus.Pending)
        {
            return ConflictResult(CodeRoundClosed, "This approval round is already closed.");
        }

        var now = DateTime.UtcNow;
        var reason = request.Reason.Trim();
        var engagement = round.Engagement!;
        var rems = engagement.Entity!.Client!.Rems!;

        task.Status = RemsApprovalTaskStatus.Rejected;
        task.DecidedOnUtc = now;
        task.RejectionReason = reason;
        _approvals.UpdateTask(task);

        // A single rejection ends the round and returns the engagement for rework.
        round.Status = RemsApprovalRoundStatus.Rejected;
        round.CompletedOnUtc = now;
        round.RejectionReason = reason;
        _approvals.UpdateRound(round);

        engagement.Status = RemsEngagementStatus.Rejected;
        _engagements.Update(engagement);

        // Notify the sender and CSE (the reason is retained on the round/task, visible to CSE + Admin).
        var recipients = new HashSet<Guid> { round.SentByUserId };
        if (rems.CSEId is { } cse)
        {
            recipients.Add(cse);
        }
        foreach (var userId in recipients)
        {
            await _notifications.DispatchAsync(new CreateNotificationDto(
                userId, NotificationType.RemsEngagementRejected,
                "A REMS engagement approval was rejected",
                $"{rems.REMSNumber} — {rems.Title}: {reason}", EntityType.Rems, rems.Id), cancellationToken);
        }
        await _activity.WriteAsync(new CreateActivityEventDto(EntityType.Rems, rems.Id, ActivityEventTypes.RemsRejected, null, reason), cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var view = await BuildTaskViewAsync(task, cancellationToken);
        return Ok(ApiResponseFactory.Success(view, "Task rejected."));
    }

    // -------------------- Approver-list generation --------------------

    /// <summary>
    /// Builds the suggested approver set (AC-REMS-018): CSE (from the request), the effective department
    /// director (engagement value, prefilled from the tenant map; skipped when unassigned), the managing
    /// shareholder (from settings), and each commission recipient. Deduped by (user, role) — the same user
    /// may hold several DISTINCT role tasks.
    /// </summary>
    private async Task<IReadOnlyList<(Guid UserId, RemsApproverRole Role)>> BuildApproverListAsync(
        REMSEngagement engagement, CancellationToken cancellationToken)
    {
        var result = new List<(Guid UserId, RemsApproverRole Role)>();

        if (engagement.Entity?.Client?.Rems?.CSEId is { } cse)
        {
            result.Add((cse, RemsApproverRole.CSE));
        }
        if (engagement.DepartmentDirectorId is { } director)
        {
            result.Add((director, RemsApproverRole.DepartmentDirector));
        }

        var settings = await _settings.GetAsync(cancellationToken);
        if (settings?.ManagingShareholderUserId is { } managingShareholder)
        {
            result.Add((managingShareholder, RemsApproverRole.ManagingShareholder));
        }

        foreach (var split in engagement.CommissionSplits.Where(s => !s.Deleted))
        {
            result.Add((split.EmployeeId, RemsApproverRole.CommissionRecipient));
        }

        // Dedup by (user, role): value tuples give structural equality.
        return result.Distinct().ToList();
    }

    private async Task<RemsApproverList> ToApproverListAsync(
        REMSEngagement engagement, IReadOnlyList<(Guid UserId, RemsApproverRole Role)> approvers, CancellationToken cancellationToken)
    {
        var names = await _users.GetFullNamesAsync(approvers.Select(a => a.UserId), cancellationToken);
        var suggestions = approvers
            .Select(a => new RemsApproverSuggestion(
                new RemsUserRef(a.UserId, names.TryGetValue(a.UserId, out var n) ? n : string.Empty), a.Role.ToString()))
            .ToList();
        return new RemsApproverList(engagement.Id, engagement.Status.ToString(), suggestions);
    }

    /// <summary>Validates the pre-approval requirements (marketing tag; audit CAF; government-audit contract + Florida flag).</summary>
    private async Task<IActionResult?> ValidateApprovalPrerequisitesAsync(REMSEngagement engagement, CancellationToken cancellationToken)
    {
        // The engagement's core placement + team + realization are mandatory. The workspace enforces this
        // on its Setup step too; this is the backstop for anything reaching the API another way.
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(engagement.Department)) missing.Add("Department");
        if (string.IsNullOrWhiteSpace(engagement.ServiceLine)) missing.Add("Service Line");
        if (engagement.EngagementExecutiveId is null) missing.Add("Engagement Executive");
        if (engagement.BillingManagerId is null) missing.Add("Billing Manager");
        if (engagement.RealizationPercentage is null) missing.Add("% Realization");
        if (missing.Count > 0)
        {
            return ConflictResult(CodeSetupIncomplete, $"Complete the engagement setup first — missing: {string.Join(", ", missing)}.");
        }

        if (!engagement.MarketingMethods.Any(m => !m.Deleted))
        {
            return ConflictResult(CodeMarketingRequired, "At least one marketing tag is required before sending for approval.");
        }

        if (RemsEngagementCodes.IsAudit(engagement.Department))
        {
            var audit = await _engagements.GetAuditDetailAsync(engagement.Id, cancellationToken);
            if (audit?.ClientAcceptanceFormMediaId is null)
            {
                return ConflictResult(CodeCafRequired, "A signed client-acceptance form is required for an audit engagement.");
            }
        }

        if (RemsEngagementCodes.IsGovernmentAudit(engagement.Department, engagement.ServiceLine))
        {
            var government = await _engagements.GetGovernmentDetailAsync(engagement.Id, cancellationToken);
            if (government is null || string.IsNullOrWhiteSpace(government.ContractNumber) || government.FloridaOnePercentStateFeeApplies is null)
            {
                return ConflictResult(CodeGovDetailRequired, "A government audit requires a contract number and the Florida 1% state-fee flag.");
            }
        }

        return null;
    }

    // -------------------- Round + task + checklist creation --------------------

    /// <summary>
    /// Creates a new approval round with a pending task per approver (each with its role checklist), locks the
    /// list, sets the engagement to PendingApproval, notifies every approver once, and logs the
    /// send/resubmission with the complete approver list. All staged in one atomic <see cref="IUnitOfWork.SaveChangesAsync"/>;
    /// the round-number uniqueness is backed by the (engagement, round-number) unique index.
    /// </summary>
    private async Task CreateRoundAsync(
        REMSEngagement engagement, IReadOnlyList<(Guid UserId, RemsApproverRole Role)> approvers,
        Guid actorId, bool isResubmission, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var rems = engagement.Entity!.Client!.Rems!;

        var roundNumber = await _approvals.GetNextRoundNumberAsync(engagement.Id, cancellationToken);
        var round = new REMSApprovalRound
        {
            Id = Guid.NewGuid(),
            REMSEngagementId = engagement.Id,
            RoundNumber = roundNumber,
            Status = RemsApprovalRoundStatus.Pending,
            SentOnUtc = now,
            SentByUserId = actorId,
        };
        await _approvals.AddRoundAsync(round, cancellationToken);

        foreach (var (userId, role) in approvers)
        {
            var task = new REMSApprovalTask
            {
                Id = Guid.NewGuid(),
                REMSApprovalRoundId = round.Id,
                ApproverId = userId,
                ApproverRole = role,
                Status = RemsApprovalTaskStatus.Pending,
            };
            await _approvals.AddTaskAsync(task, cancellationToken);

            var order = 1;
            foreach (var label in RemsApprovalChecklistCatalog.For(role))
            {
                await _approvals.AddChecklistItemAsync(new REMSApprovalChecklistItem
                {
                    Id = Guid.NewGuid(),
                    REMSApprovalTaskId = task.Id,
                    DisplayOrder = order++,
                    Label = label,
                    IsCompleted = false,
                }, cancellationToken);
            }
        }

        engagement.Status = RemsEngagementStatus.PendingApproval;
        _engagements.Update(engagement);

        // Notify every approver (once per user, even if they hold multiple role tasks).
        foreach (var userId in approvers.Select(a => a.UserId).Distinct())
        {
            await _notifications.DispatchAsync(new CreateNotificationDto(
                userId, NotificationType.RemsApprovalRequested,
                isResubmission ? "A REMS engagement was resubmitted for your approval" : "A REMS engagement needs your approval",
                $"{rems.REMSNumber} — {rems.Title}", EntityType.Rems, rems.Id), cancellationToken);
        }

        var listText = string.Join(", ", approvers.Select(a => $"{a.UserId}:{a.Role}"));
        await _activity.WriteAsync(new CreateActivityEventDto(
            EntityType.Rems, rems.Id,
            isResubmission ? ActivityEventTypes.RemsApprovalResubmitted : ActivityEventTypes.RemsApprovalSent,
            null, listText), cancellationToken);

        // One atomic commit: round + tasks + checklists + engagement status + notifications + activity.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // -------------------- Role-scoped task view --------------------

    private async Task<RemsApprovalTaskView> BuildTaskViewAsync(REMSApprovalTask task, CancellationToken cancellationToken)
    {
        var round = task.Round!;
        var engagement = round.Engagement!;
        var entity = engagement.Entity!;
        var client = entity.Client!;
        var rems = client.Rems!;

        var names = await _users.GetFullNamesAsync(EngagementUserIds(engagement), cancellationToken);

        RemsApprovalClientView? clientView = null;
        decimal? fee = null;
        decimal? realization = null;
        RemsUserRef? director = null;
        RemsUserRef? executive = null;
        RemsUserRef? billingManager = null;
        IReadOnlyList<RemsCommissionSplitView>? commission = null;
        IReadOnlyList<Guid>? marketing = null;

        switch (task.ApproverRole)
        {
            case RemsApproverRole.CSE:
                // Full client + engagement placement (fee/realization are reserved to director/managing shareholder).
                clientView = FullClient(client);
                director = RemsWorkspaceMapper.UserRef(engagement.DepartmentDirectorId, names);
                executive = RemsWorkspaceMapper.UserRef(engagement.EngagementExecutiveId, names);
                billingManager = RemsWorkspaceMapper.UserRef(engagement.BillingManagerId, names);
                marketing = engagement.MarketingMethods.Where(m => !m.Deleted).Select(m => m.MarketingMethodId).ToList();
                break;

            case RemsApproverRole.DepartmentDirector:
            case RemsApproverRole.ManagingShareholder:
                // Review data including the fee estimate and realization (AC-REMS-019.10).
                clientView = FullClient(client);
                fee = engagement.FirstYearFeeEstimate;
                realization = engagement.RealizationPercentage;
                director = RemsWorkspaceMapper.UserRef(engagement.DepartmentDirectorId, names);
                executive = RemsWorkspaceMapper.UserRef(engagement.EngagementExecutiveId, names);
                billingManager = RemsWorkspaceMapper.UserRef(engagement.BillingManagerId, names);
                marketing = engagement.MarketingMethods.Where(m => !m.Deleted).Select(m => m.MarketingMethodId).ToList();
                break;

            case RemsApproverRole.CommissionRecipient:
                // Commission-decision data: the client basics + the commission splits.
                clientView = BasicClient(client);
                commission = engagement.CommissionSplits
                    .Where(s => !s.Deleted)
                    .Select(s => new RemsCommissionSplitView(s.Id, RemsWorkspaceMapper.UserRef(s.EmployeeId, names)!, s.CommissionPercentage))
                    .ToList();
                break;
        }

        var engagementView = new RemsApprovalEngagementView(
            engagement.Id, rems.Id, rems.REMSNumber, entity.Name, engagement.Department, engagement.ServiceLine,
            clientView, fee, realization, director, executive, billingManager, commission, marketing);

        var checklist = task.ChecklistItems
            .Where(i => !i.Deleted)
            .OrderBy(i => i.DisplayOrder)
            .Select(i => new RemsChecklistItemView(i.Id, i.DisplayOrder, i.Label, i.IsCompleted, i.CompletedOnUtc))
            .ToList();

        var canDecide = task.Status == RemsApprovalTaskStatus.Pending && round.Status == RemsApprovalRoundStatus.Pending;

        return new RemsApprovalTaskView(
            task.Id, round.Id, round.RoundNumber, task.ApproverRole.ToString(), task.Status.ToString(),
            task.DecidedOnUtc, task.RejectionReason, canDecide, checklist, engagementView);
    }

    private static RemsApprovalClientView FullClient(REMSClient client)
    {
        var entities = client.Entities
            .Where(e => !e.Deleted)
            .OrderByDescending(e => e.IsMainEntity)
            .ThenBy(e => e.Name)
            .Select(e => new RemsApprovalEntitySummary(e.Id, e.Name, e.EIN, e.IsMainEntity))
            .ToList();
        return new RemsApprovalClientView(client.Id, client.Name, client.Email, client.MobileNumber, client.ReferralSource, entities);
    }

    private static RemsApprovalClientView BasicClient(REMSClient client)
        => new(client.Id, client.Name, client.Email, client.MobileNumber, client.ReferralSource, Array.Empty<RemsApprovalEntitySummary>());

    private static IReadOnlyCollection<Guid> EngagementUserIds(REMSEngagement engagement)
    {
        var ids = new HashSet<Guid>();
        if (engagement.DepartmentDirectorId is { } d) ids.Add(d);
        if (engagement.EngagementExecutiveId is { } x) ids.Add(x);
        if (engagement.BillingManagerId is { } b) ids.Add(b);
        foreach (var s in engagement.CommissionSplits.Where(s => !s.Deleted))
        {
            ids.Add(s.EmployeeId);
        }
        return ids;
    }

    private IActionResult ConflictResult(string code, string message)
        => StatusCode(StatusCodes.Status409Conflict, ApiResponseFactory.Error(code, message, message));
}
