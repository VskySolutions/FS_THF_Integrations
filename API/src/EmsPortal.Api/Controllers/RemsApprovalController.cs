using System.Text.Json;
using EmsPortal.Api.Models.Rems;
using EmsPortal.Api.Security;
using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Application.Abstractions.UniversalFeatures;
using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;
using EmsPortal.Shared.Contracts;
using EmsPortal.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmsPortal.Api.Controllers;

/// <summary>
/// REMS approval workflow backend (WO-114 Part C). Staff route an engagement for approval and manage
/// resubmission (<see cref="Permissions.RemsApprovalsSend"/>).
/// <para>
/// Acting on an approval task needs no permission — only that the task is YOURS. Approver-ness is data,
/// not a role: a user becomes an approver by being the request's CSE, a commission recipient, or someone
/// added on the Approval tab, and any role can find itself in one of those seats (a commission recipient
/// is routinely a Partner). Gating on a role-derived permission meant the role assignment and the
/// engagement data had to agree, and when they did not the approver simply could not reach a task that
/// had been created for them — and since a round completes only when EVERY task is approved, the
/// engagement stalled indefinitely with no way to unstick it from the UI.
/// </para>
/// The boundary is unchanged and is the one that matters: every own-task endpoint requires an
/// authenticated caller and re-checks <c>ApproverId == caller</c>, so a task that is not the caller's own
/// is a 404 and the list only ever returns their own rows. Tenant isolation is ambient.
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
    private const string CodeApproversLocked = "REMS_APPROVERS_LOCKED";

    private const string MarketingSetKey = "REMSMarketing_MarketingMethods.MarketingMethodId";
    private const string TaxFormSetKey = "REMS.TaxForm";

    private readonly IRemsRepository _rems;
    private readonly IRemsEngagementRepository _engagements;
    private readonly IRemsApprovalRepository _approvals;
    private readonly IRemsClientRepository _clients;
    private readonly IRemsSettingsRepository _settings;
    private readonly IMediaRepository _media;
    private readonly IOptionSetRepository _optionSets;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IActivityEventWriter _activity;
    private readonly INotificationDispatcher _notifications;

    public RemsApprovalController(
        IRemsRepository rems,
        IRemsEngagementRepository engagements,
        IRemsApprovalRepository approvals,
        IRemsClientRepository clients,
        IRemsSettingsRepository settings,
        IMediaRepository media,
        IOptionSetRepository optionSets,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IActivityEventWriter activity,
        INotificationDispatcher notifications)
    {
        _rems = rems;
        _engagements = engagements;
        _approvals = approvals;
        _clients = clients;
        _settings = settings;
        _media = media;
        _optionSets = optionSets;
        _users = users;
        _unitOfWork = unitOfWork;
        _activity = activity;
        _notifications = notifications;
    }

    // -------------------- Suggested approvers (live) --------------------

    /// <summary>
    /// The engagement's approver list (AC-REMS-018): the automatic approvers — the CSE and every commission
    /// recipient — plus anyone added on the Approval tab. Updates until the round is sent.
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
        return Ok(ApiResponseFactory.Success(list, "REMS approvers retrieved."));
    }

    /// <summary>
    /// The users selectable as extra approvers: everyone holding the <c>Approver</c> role in the active
    /// tenant, with their job title for the picker label.
    /// </summary>
    [HttpGet("engagements/{id:guid}/approver-options")]
    [RequirePermission(Permissions.RemsEngagementsManage)]
    [ProducesResponseType<ApiResponse<IEnumerable<RemsApproverOption>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproverOptions(Guid id, CancellationToken cancellationToken)
    {
        if (await _engagements.GetWithContextAsync(id, cancellationToken) is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS engagement not found."));
        }
        if (User.GetActiveTenantId() is not { } tenantId)
        {
            return Ok(ApiResponseFactory.Success(Array.Empty<RemsApproverOption>(), "No active tenant."));
        }

        var options = await ApproverOptionsAsync(tenantId, cancellationToken);
        return Ok(ApiResponseFactory.Success(options, "REMS approver options retrieved."));
    }

    /// <summary>
    /// Replaces the engagement's ADDED approvers (AC-REMS-018). Editable only while the approver list is
    /// unlocked — once a round is sent the list is fixed. An empty set removes the additions; the automatic
    /// approvers route either way.
    /// </summary>
    [HttpPut("engagements/{id:guid}/approvers")]
    [RequirePermission(Permissions.RemsEngagementsManage)]
    [ProducesResponseType<ApiResponse<RemsApproverList>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetApprovers(Guid id, [FromBody] SetRemsApproversRequest request, CancellationToken cancellationToken)
    {
        var engagement = await _engagements.GetWithContextAsync(id, cancellationToken);
        if (engagement is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS engagement not found."));
        }
        if (engagement.Status is not (RemsEngagementStatus.Draft or RemsEngagementStatus.Rejected))
        {
            return ConflictResult(CodeApproversLocked, "The approver list is locked once the engagement has been sent for approval.");
        }

        var requested = (request.UserIds ?? new List<Guid>()).Distinct().ToList();

        // Every pick must be one the picker actually offered. Validating against the option set rather than
        // "is a real user" keeps the choice inside this tenant: an arbitrary id must never become a task.
        if (requested.Count > 0)
        {
            if (User.GetActiveTenantId() is not { } tenantId)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponseFactory.Forbidden("No active tenant context."));
            }

            var allowed = (await ApproverOptionsAsync(tenantId, cancellationToken)).Select(o => o.UserId).ToHashSet();
            if (requested.Any(uid => !allowed.Contains(uid)))
            {
                return BadRequest(ApiResponseFactory.Error(
                    ApiErrorCodes.ValidationFailed, "Validation failed.", "One or more selected approvers do not hold the Approver role."));
            }
        }

        // Reconcile to exactly the requested set.
        var existing = await _engagements.ListApproversAsync(id, cancellationToken);
        foreach (var row in existing.Where(r => !requested.Contains(r.UserId)))
        {
            _engagements.RemoveApprover(row);
        }
        foreach (var userId in requested.Where(uid => existing.All(r => r.UserId != uid)))
        {
            await _engagements.AddApproverAsync(new REMSEngagementApprover
            {
                Id = Guid.NewGuid(),
                REMSEngagementId = id,
                UserId = userId,
            }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var approvers = await BuildApproverListAsync(engagement, cancellationToken);
        var list = await ToApproverListAsync(engagement, approvers, cancellationToken);
        return Ok(ApiResponseFactory.Success(list, "REMS approvers updated."));
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

    /// <summary>
    /// The caller's own approval tasks (pending and historical), newest round first (AC-REMS-019), paged and
    /// filtered server-side: <paramref name="search"/> over the REMS number, client and entity name, plus
    /// optional <paramref name="role"/> / <paramref name="status"/>. Each row carries the round's
    /// approved/rejected/total counts so the inbox can show its progress — the row is still the caller's own
    /// task, and no other approver's identity is exposed here.
    /// </summary>
    /// <summary>
    /// Every approval round on an engagement, oldest first — who sent it, what each approver decided, why
    /// they declined, how far their checklist got, and how the declines stood against the threshold.
    /// <para>
    /// Gated on reading REMS requests rather than on managing engagements: the initiator has to be able to
    /// see why a round came back, and reworking the setup is now their job.
    /// </para>
    /// </summary>
    [HttpGet("engagements/{id:guid}/approval/history")]
    [RequirePermission(Permissions.RemsRequestsRead)]
    [ProducesResponseType<ApiResponse<IEnumerable<RemsApprovalRoundHistory>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> History(Guid id, CancellationToken cancellationToken)
    {
        if (await _engagements.GetByIdAsync(id, cancellationToken) is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS engagement not found."));
        }

        var rounds = await _approvals.GetRoundsByEngagementAsync(id, cancellationToken);
        var names = await _users.GetFullNamesAsync(
            rounds.SelectMany(r => r.Tasks.Select(t => t.ApproverId)).Concat(rounds.Select(r => r.SentByUserId)),
            cancellationToken);

        string Name(Guid userId) => names.TryGetValue(userId, out var n) ? n : "Unknown user";

        var history = rounds
            .OrderBy(r => r.RoundNumber)
            .Select(r =>
            {
                var tasks = r.Tasks.Where(t => !t.Deleted).ToList();
                return new RemsApprovalRoundHistory(
                    r.Id, r.RoundNumber, r.Status.ToString(), r.SentOnUtc, Name(r.SentByUserId), r.CompletedOnUtc,
                    RemsApprovalThreshold.EffectiveFor(tasks.Count),
                    tasks.Count(t => t.Status == RemsApprovalTaskStatus.Rejected),
                    tasks
                        .OrderBy(t => t.ApproverRole)
                        .ThenBy(t => t.CreatedOnUtc)
                        .Select(t =>
                        {
                            var items = t.ChecklistItems.Where(i => !i.Deleted).ToList();
                            return new RemsApprovalRoundDecision(
                                t.Id, Name(t.ApproverId), t.ApproverRole.ToString(), t.Status.ToString(),
                                t.DecidedOnUtc, t.RejectionReason,
                                items.Count(i => i.IsCompleted), items.Count);
                        })
                        .ToList());
            })
            .ToList();

        return Ok(ApiResponseFactory.Success(history, "REMS approval history retrieved."));
    }

    [HttpGet("approval-tasks")]
    [Authorize]
    [ProducesResponseType<ApiResponse<IEnumerable<RemsApprovalTaskRow>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> MyTasks(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? role = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } me)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);

        // An unparseable role/status is treated as "no filter" rather than an error: these arrive from a
        // picker, and a stale value should show everything, not fail the page.
        RemsApproverRole? roleFilter = Enum.TryParse<RemsApproverRole>(role, ignoreCase: true, out var r) ? r : null;
        RemsApprovalTaskStatus? statusFilter =
            Enum.TryParse<RemsApprovalTaskStatus>(status, ignoreCase: true, out var s) ? s : null;

        var (tasks, total) = await _approvals.ListTasksByApproverAsync(
            new RemsApprovalTaskQuery(me, search, roleFilter, statusFilter, page, limit), cancellationToken);

        var auditNames = await _users.GetFullNamesAsync(
            tasks.SelectMany(t => new[] { t.CreatedById, t.UpdatedById })
                .Where(id => id.HasValue).Select(id => id!.Value),
            cancellationToken);
        string? NameOf(Guid? id) => id is { } uid && auditNames.TryGetValue(uid, out var n) ? n : null;

        var rows = tasks.Select(t =>
        {
            var round = t.Round!;
            var engagement = round.Engagement!;
            var rems = engagement.Rems!;
            var client = ClientOf(engagement);
            return new RemsApprovalTaskRow(
                t.Id, round.Id, round.RoundNumber, t.ApproverRole.ToString(), t.Status.ToString(),
                round.SentOnUtc, t.DecidedOnUtc, round.Status.ToString(),
                // The client's name as they gave it on intake, falling back to the name the request was
                // raised under when the row is somehow reached before a submission exists.
                engagement.Id, rems.Id, rems.REMSNumber, client?.Name ?? rems.RequestedClientName,
                round.Tasks.Count(x => x.Status == RemsApprovalTaskStatus.Approved),
                round.Tasks.Count(x => x.Status == RemsApprovalTaskStatus.Rejected),
                round.Tasks.Count,
                NameOf(t.CreatedById), t.CreatedOnUtc, NameOf(t.UpdatedById), t.UpdatedOnUtc);
        });
        return Ok(ApiResponseFactory.Paginated(rows, "REMS approval tasks retrieved.", page, limit, total));
    }

    /// <summary>
    /// The caller's own task with the full review packet (AC-REMS-019.9): the originating request, the
    /// client, the entity under review, the complete engagement setup with its audit/government/tax detail,
    /// the marketing tags, the commission splits, the round's other decisions, and the caller's checklist —
    /// the same case staff assembled in the engagement workspace, since that is what is being approved.
    /// The fee estimate and realization remain reserved to the Department Director and Managing Shareholder
    /// (AC-REMS-019.10). Not the caller's own task =&gt; 404.
    /// </summary>
    [HttpGet("approval-tasks/{taskId:guid}")]
    [Authorize]
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
    [Authorize]
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
    [Authorize]
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
        var rems = engagement.Rems!;

        task.Status = RemsApprovalTaskStatus.Approved;
        task.DecidedOnUtc = now;
        _approvals.UpdateTask(task);
        await _activity.WriteAsync(new CreateActivityEventDto(EntityType.Rems, rems.Id, ActivityEventTypes.RemsApproved, null, task.ApproverRole.ToString()), cancellationToken);

        // The round is settled once nobody is still deciding. It cannot require EVERY task to be approved
        // any more: a single decline no longer closes the round, so a round holding one decline and
        // otherwise approvals would never satisfy that test and would sit open forever with no pending task
        // left to move it. Below the decline threshold the objection is outvoted and the round carries.
        // (A round that REACHED the threshold was closed by Reject and is not Pending, so it cannot land
        // here.) Flip the round + engagement and raise the full-approval notification EXACTLY ONCE to
        // everyone involved (AC-REMS-019.1/12).
        var fullyApproved = round.Tasks.All(t => t.Id == task.Id || t.Status != RemsApprovalTaskStatus.Pending);
        if (fullyApproved)
        {
            round.Status = RemsApprovalRoundStatus.Approved;
            round.CompletedOnUtc = now;
            _approvals.UpdateRound(round);

            engagement.Status = RemsEngagementStatus.Approved;
            _engagements.Update(engagement);
            SyncRequestStatus(rems, engagement);

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
                    $"{rems.REMSNumber} — {rems.RequestedClientName}", EntityType.Rems, rems.Id), cancellationToken);
            }
            await _activity.WriteAsync(new CreateActivityEventDto(EntityType.Rems, rems.Id, ActivityEventTypes.RemsFullyApproved), cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var view = await BuildTaskViewAsync(task, cancellationToken);
        return Ok(ApiResponseFactory.Success(view, fullyApproved ? "Task approved; engagement fully approved." : "Task approved."));
    }

    /// <summary>
    /// Decline the caller's own task with a required reason (AC-REMS-020).
    /// <para>
    /// One decline no longer ends a round. The round closes when
    /// <see cref="RemsApprovalThreshold.Declines"/> approvers have declined, so a lone objector is outvoted
    /// rather than decisive — with four approvals against one decline the engagement is approved over that
    /// objection. Approvers still pending when the threshold is reached are marked
    /// <see cref="RemsApprovalTaskStatus.Superseded"/> rather than left Pending on a closed round, which
    /// would read as though they never responded.
    /// </para>
    /// <para>
    /// A closed round sends the request back to its INITIATOR to rework the engagement setup, not to the
    /// admin. Each decliner's own reason is the record of why — the round's single
    /// <c>RejectionReason</c> cannot hold several, so it carries only the decline that closed the round.
    /// </para>
    /// </summary>
    [HttpPost("approval-tasks/{taskId:guid}/reject")]
    [Authorize]
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
        var rems = engagement.Rems!;

        task.Status = RemsApprovalTaskStatus.Rejected;
        task.DecidedOnUtc = now;
        task.RejectionReason = reason;
        _approvals.UpdateTask(task);
        await _activity.WriteAsync(new CreateActivityEventDto(EntityType.Rems, rems.Id, ActivityEventTypes.RemsRejected, null, reason), cancellationToken);

        // Counted over the round's own tasks, with this one substituted: it is not committed yet, so the
        // stored copy still reads Pending.
        var declines = round.Tasks.Count(t => t.Id == task.Id || t.Status == RemsApprovalTaskStatus.Rejected);
        var threshold = RemsApprovalThreshold.EffectiveFor(round.Tasks.Count);
        var closesRound = declines >= threshold;

        if (!closesRound)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            var openView = await BuildTaskViewAsync(task, cancellationToken);
            return Ok(ApiResponseFactory.Success(
                openView,
                $"Declined. {declines} of {threshold} declines needed to send this back — the round is still open."));
        }

        round.Status = RemsApprovalRoundStatus.Rejected;
        round.CompletedOnUtc = now;
        round.RejectionReason = reason;
        _approvals.UpdateRound(round);

        // Everyone who had not decided by the time the round closed. Their task is over, but they did not
        // decline it — Superseded is the difference.
        foreach (var pending in round.Tasks.Where(t => t.Id != task.Id && t.Status == RemsApprovalTaskStatus.Pending))
        {
            pending.Status = RemsApprovalTaskStatus.Superseded;
            pending.DecidedOnUtc = now;
            _approvals.UpdateTask(pending);
        }

        engagement.Status = RemsEngagementStatus.Rejected;
        _engagements.Update(engagement);
        SyncRequestStatus(rems, engagement);

        // Every decline's own reason, so the notice explains the round rather than only its last vote.
        var reasons = round.Tasks
            .Where(t => t.Id == task.Id || t.Status == RemsApprovalTaskStatus.Rejected)
            .Select(t => t.Id == task.Id ? reason : t.RejectionReason)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToList();

        // The initiator owns the rework now, so they are told first; the sender and CSE still need to know
        // the round is over.
        var recipients = new HashSet<Guid> { round.SentByUserId };
        if (rems.CSEId is { } cse)
        {
            recipients.Add(cse);
        }
        if (rems.CreatedById is { } initiator)
        {
            recipients.Add(initiator);
        }
        if (rems.AdminAssignedToId is { } admin)
        {
            recipients.Add(admin);
        }
        foreach (var userId in recipients)
        {
            await _notifications.DispatchAsync(new CreateNotificationDto(
                userId, NotificationType.RemsEngagementRejected,
                "A REMS engagement approval round was declined",
                $"{rems.REMSNumber} — {rems.RequestedClientName}: {string.Join(" | ", reasons)}", EntityType.Rems, rems.Id), cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var view = await BuildTaskViewAsync(task, cancellationToken);
        return Ok(ApiResponseFactory.Success(view, "Declined. The round is closed and the request is back with its initiator."));
    }

    // -------------------- Request-status roll-up --------------------

    /// <summary>
    /// Re-derives the REQUEST status from the engagements underneath it, so the request row reports the stage
    /// the work is actually at instead of staying on "Engagement Setup" for the whole approval cycle — which
    /// is what left a requester with no way to tell that their request was sitting with the approvers.
    /// <para>
    /// This used to be a roll-up across one engagement per entity. A request now carries exactly one, so it
    /// is a straight translation — <paramref name="current"/> is that engagement, not yet committed, so its
    /// in-memory status is what the request follows.
    /// </para>
    /// <para>
    /// A rejected engagement sends the request to the INITIATOR rather than back to the admin: enough
    /// approvers declined to close the round, and reworking the setup is the initiator's job. The admin
    /// sees it again when they hand it back for confirmation.
    /// </para>
    /// The REMS row is loaded tracked (via the task/engagement context include), so mutating its status is
    /// picked up by change tracking and committed with the rest of the operation.
    /// </summary>
    private static void SyncRequestStatus(REMS rems, REMSEngagement current)
        => rems.Status = current.Status switch
        {
            RemsEngagementStatus.Approved => RemsRequestStatuses.Approved,
            RemsEngagementStatus.Rejected => RemsRequestStatuses.ChangesRequested,
            RemsEngagementStatus.PendingApproval => RemsRequestStatuses.PendingApproval,
            // Back to Draft only happens if an engagement is reset, which nothing does today. Admin Review
            // is where such a request belongs: the client's answers are in and somebody has to look at it.
            _ => RemsRequestStatuses.AdminReview,
        };

    // -------------------- Approver-list generation --------------------

    /// <summary>
    /// The approver set to route to (AC-REMS-018). A selection saved on the Approval tab wins; with none
    /// saved this falls back to the default — the CSE and every commission recipient. Either way each
    /// user's role comes from <see cref="RoleFor"/> rather than from storage.
    /// </summary>
    private async Task<IReadOnlyList<(Guid UserId, RemsApproverRole Role)>> BuildApproverListAsync(
        REMSEngagement engagement, CancellationToken cancellationToken)
    {
        var picked = await _engagements.ListApproversAsync(engagement.Id, cancellationToken);

        // Added approvers come ON TOP of the automatic ones — picking someone never removes the CSE or a
        // commission recipient, who approve by virtue of their standing on the engagement.
        var userIds = AutomaticApproverIds(engagement).Concat(picked.Select(a => a.UserId));

        var settings = await _settings.GetAsync(cancellationToken);
        return userIds
            .Distinct()
            .Select(userId => (UserId: userId, Role: RoleFor(engagement, settings?.ManagingShareholderUserId, userId)))
            .ToList();
    }

    /// <summary>
    /// The users the Approval tab offers as EXTRA approvers: those holding the <c>Approver</c> role in the
    /// tenant. The automatic approvers are deliberately absent — they are already routed to, so listing
    /// them would invite picking someone who is on the list either way.
    /// </summary>
    private async Task<IReadOnlyList<RemsApproverOption>> ApproverOptionsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var candidates = await _users.ListByTenantRolesAsync(tenantId, new[] { Roles.Approver }, cancellationToken);
        var names = await _users.GetFullNamesAsync(candidates.Select(u => u.Id), cancellationToken);

        return candidates
            .Select(u => new RemsApproverOption(
                u.Id, names.TryGetValue(u.Id, out var n) ? n : u.DisplayName, u.Person?.JobTitle, u.Email))
            .OrderBy(o => o.Name)
            .ToList();
    }

    /// <summary>
    /// The approvers every engagement routes to regardless of what is picked: the CSE and each commission
    /// recipient. The department director and the managing shareholder are not automatic — they are added
    /// on the Approval tab like anyone else.
    /// </summary>
    /// <summary>
    /// The client materialised from the request's intake, or null before the client has submitted. Clients
    /// is a collection at the EF level — one active row, enforced by a filtered unique index — because the
    /// engagement now hangs off the request rather than off the client's entity.
    /// </summary>
    private static REMSClient? ClientOf(REMSEngagement engagement)
        => engagement.Rems?.Clients.FirstOrDefault(c => !c.Deleted);

    private static List<Guid> AutomaticApproverIds(REMSEngagement engagement)
    {
        var ids = new List<Guid>();
        if (engagement.Rems?.CSEId is { } cse)
        {
            ids.Add(cse);
        }
        ids.AddRange(engagement.CommissionSplits.Where(s => !s.Deleted).Select(s => s.EmployeeId));
        return ids;
    }

    /// <summary>
    /// The role a user acts under on this engagement, most specific first. Someone with no standing of their
    /// own — a hand-picked approver — reviews as a plain <see cref="RemsApproverRole.Approver"/>. Derived
    /// rather than stored so a saved list keeps up when the CSE or the commission recipients change.
    /// </summary>
    private static RemsApproverRole RoleFor(REMSEngagement engagement, Guid? managingShareholderId, Guid userId)
    {
        if (engagement.Rems?.CSEId == userId)
        {
            return RemsApproverRole.CSE;
        }
        if (engagement.DepartmentDirectorId == userId)
        {
            return RemsApproverRole.DepartmentDirector;
        }
        if (managingShareholderId == userId)
        {
            return RemsApproverRole.ManagingShareholder;
        }
        if (engagement.CommissionSplits.Any(s => !s.Deleted && s.EmployeeId == userId))
        {
            return RemsApproverRole.CommissionRecipient;
        }
        return RemsApproverRole.Approver;
    }

    private async Task<RemsApproverList> ToApproverListAsync(
        REMSEngagement engagement, IReadOnlyList<(Guid UserId, RemsApproverRole Role)> approvers,
        CancellationToken cancellationToken)
    {
        var names = await _users.GetFullNamesAsync(approvers.Select(a => a.UserId), cancellationToken);
        var suggestions = approvers
            .Select(a => new RemsApproverSuggestion(
                new RemsUserRef(a.UserId, names.TryGetValue(a.UserId, out var n) ? n : string.Empty), a.Role.ToString()))
            .ToList();

        // Only the ADDED approvers — the picker must not show back the people who are on the list anyway.
        var selected = (await _engagements.ListApproversAsync(engagement.Id, cancellationToken))
            .Select(a => a.UserId)
            .ToList();

        return new RemsApproverList(engagement.Id, engagement.Status.ToString(), suggestions, selected);
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
        var rems = engagement.Rems!;

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
        SyncRequestStatus(rems, engagement);

        // Notify every approver (once per user, even if they hold multiple role tasks).
        foreach (var userId in approvers.Select(a => a.UserId).Distinct())
        {
            await _notifications.DispatchAsync(new CreateNotificationDto(
                userId, NotificationType.RemsApprovalRequested,
                isResubmission ? "A REMS engagement was resubmitted for your approval" : "A REMS engagement needs your approval",
                $"{rems.REMSNumber} — {rems.RequestedClientName}", EntityType.Rems, rems.Id), cancellationToken);
        }

        var listText = string.Join(", ", approvers.Select(a => $"{a.UserId}:{a.Role}"));
        await _activity.WriteAsync(new CreateActivityEventDto(
            EntityType.Rems, rems.Id,
            isResubmission ? ActivityEventTypes.RemsApprovalResubmitted : ActivityEventTypes.RemsApprovalSent,
            null, listText), cancellationToken);

        // One atomic commit: round + tasks + checklists + engagement status + notifications + activity.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // -------------------- Task review packet --------------------

    /// <summary>
    /// Builds the approver's review packet: the request that started it, the client, the entity under review
    /// with its addresses and contacts, the full engagement setup (audit/government/tax detail included), the
    /// marketing tags and commission splits, and the round's other decisions — the same material staff filled
    /// in across the workspace's four tabs, because that is what the approver is signing off on.
    /// <para>
    /// Option-set references (marketing methods, tax forms) are resolved to LABELS here: the approver roles
    /// do not carry <c>optionSets.read</c>, so ids would be unreadable on that screen. The one thing still
    /// scoped by role is fee/realization (AC-REMS-019.10) — see <see cref="MaySeeFinancials"/>.
    /// </para>
    /// </summary>
    private async Task<RemsApprovalTaskView> BuildTaskViewAsync(REMSApprovalTask task, CancellationToken cancellationToken)
    {
        var round = task.Round!;
        var engagement = round.Engagement!;
        var client = ClientOf(engagement)!;

        // The task-context graph stops at the client's entities: their addresses/contacts, the conditional
        // engagement detail and the request's files each need their own load. The engagement no longer
        // names an entity, so the packet reviews the client's MAIN one — the business being engaged.
        var mainEntity = client.Entities.FirstOrDefault(e => !e.Deleted && e.IsMainEntity)
            ?? client.Entities.FirstOrDefault(e => !e.Deleted);
        var entity = mainEntity is null
            ? null
            : await _clients.GetEntityAsync(mainEntity.Id, cancellationToken) ?? mainEntity;
        var rems = await _rems.GetByIdAsync(client.REMSId, cancellationToken) ?? engagement.Rems!;
        var audit = await _engagements.GetAuditDetailAsync(engagement.Id, cancellationToken);
        var government = await _engagements.GetGovernmentDetailAsync(engagement.Id, cancellationToken);
        var tax = await _engagements.GetTaxDetailAsync(engagement.Id, cancellationToken);
        var formState = (await _rems.GetFormStatesAsync(new[] { rems.Id }, cancellationToken)).FirstOrDefault();

        var names = await _users.GetFullNamesAsync(
            EngagementUserIds(engagement)
                .Concat(round.Tasks.Select(t => t.ApproverId))
                .Append(round.SentByUserId)
                .Concat(new[] { rems.AdminAssignedToId, rems.CSEId, rems.CreatedById }
                    .Where(id => id.HasValue).Select(id => id!.Value)),
            cancellationToken);

        var (emsFormState, clientSubmissionState) = RemsWorkspaceMapper.FormState(formState);
        var requestView = new RemsApprovalRequestView(
            rems.Id, rems.REMSNumber, rems.Description, rems.RequestedClientName,
            rems.Type, rems.Status, rems.CustomerEmail, rems.CustomerMobileNumber,
            formState?.IndustryGroup, emsFormState, clientSubmissionState,
            RemsWorkspaceMapper.UserRef(rems.AdminAssignedToId, names),
            RemsWorkspaceMapper.UserRef(rems.CSEId, names),
            rems.CreatedById is { } creator && names.TryGetValue(creator, out var creatorName) ? creatorName : null,
            rems.CreatedOnUtc,
            rems.Files.Where(f => !f.Deleted)
                .Select(f => new RemsFileRef(
                    f.Id, f.MediaId, f.Media?.OriginalFileName, f.Media?.MimeType, f.Media?.FileSize, f.Media?.PublicUrl))
                .ToList());

        var engagementView = await BuildApprovalEngagementViewAsync(
            task, engagement, client, entity, audit, government, tax, names, cancellationToken);

        // Decided first, oldest decision at the top, so the list reads as the round's history: who signed
        // off first, then next, with whoever has yet to decide gathered at the bottom. Role only breaks
        // ties among the undecided, who share a null timestamp.
        var decisions = round.Tasks
            .OrderBy(t => t.DecidedOnUtc.HasValue ? 0 : 1)
            .ThenBy(t => t.DecidedOnUtc)
            .ThenBy(t => t.ApproverRole)
            .Select(t => new RemsApprovalDecisionView(
                t.Id, RemsWorkspaceMapper.UserRef(t.ApproverId, names)!, t.ApproverRole.ToString(),
                t.Status.ToString(), t.DecidedOnUtc, t.RejectionReason, t.Id == task.Id))
            .ToList();

        var roundView = new RemsApprovalRoundView(
            round.Id, round.RoundNumber, round.Status.ToString(), round.SentOnUtc,
            RemsWorkspaceMapper.UserRef(round.SentByUserId, names), round.CompletedOnUtc, round.RejectionReason,
            decisions);

        var checklist = task.ChecklistItems
            .Where(i => !i.Deleted)
            .OrderBy(i => i.DisplayOrder)
            .Select(i => new RemsChecklistItemView(i.Id, i.DisplayOrder, i.Label, i.IsCompleted, i.CompletedOnUtc))
            .ToList();

        var canDecide = task.Status == RemsApprovalTaskStatus.Pending && round.Status == RemsApprovalRoundStatus.Pending;

        return new RemsApprovalTaskView(
            task.Id, round.Id, round.RoundNumber, task.ApproverRole.ToString(), task.Status.ToString(),
            task.DecidedOnUtc, task.RejectionReason, canDecide, checklist, requestView, engagementView, roundView);
    }

    private async Task<RemsApprovalEngagementView> BuildApprovalEngagementViewAsync(
        REMSApprovalTask task,
        REMSEngagement engagement,
        REMSClient client,
        REMSEntity entity,
        REMSEngagementAuditDetail? audit,
        REMSEngagementGovernmentDetail? government,
        REMSEngagementTaxDetail? tax,
        IReadOnlyDictionary<Guid, string> names,
        CancellationToken cancellationToken)
    {
        var maySeeFinancials = MaySeeFinancials(task.ApproverRole);

        var marketingIds = engagement.MarketingMethods.Where(m => !m.Deleted).Select(m => m.MarketingMethodId).ToList();
        var marketing = await ResolveOptionRefsAsync(MarketingSetKey, marketingIds, cancellationToken);

        RemsApprovalAuditDetailView? auditView = null;
        if (audit is not null)
        {
            // Resolve the signed CAF to something the approver can actually open — "a media id is on file"
            // is not a document anyone can review.
            var media = audit.ClientAcceptanceFormMediaId is { } mediaId
                ? await _media.GetByIdAsync(mediaId, cancellationToken)
                : null;
            auditView = new RemsApprovalAuditDetailView(
                audit.Id, audit.ClientAcceptanceFormMediaId, media?.OriginalFileName, media?.PublicUrl);
        }

        RemsApprovalTaxDetailView? taxView = null;
        if (tax is not null)
        {
            var taxFormIds = tax.TaxForms.Where(f => !f.Deleted).Select(f => f.TaxFormId).ToList();
            taxView = new RemsApprovalTaxDetailView(
                tax.Id, tax.FiscalYearEnd, RemsTaxDueDates.TryDeserialize(tax.CalculatedDueDates),
                await ResolveOptionRefsAsync(TaxFormSetKey, taxFormIds, cancellationToken));
        }

        var clientView = new RemsApprovalClientView(
            client.Id, client.Name, client.Email, client.MobileNumber, client.ReferralSource,
            client.BillingContactName, client.BillingEmail,
            client.Entities
                .Where(e => !e.Deleted)
                .OrderByDescending(e => e.IsMainEntity)
                .ThenBy(e => e.Name)
                .Select(e => new RemsApprovalEntitySummary(e.Id, e.Name, e.EIN, e.IsMainEntity))
                .ToList());

        var entityView = new RemsApprovalEntityView(
            entity.Id, entity.Name, entity.EIN, entity.IsMainEntity,
            entity.Addresses.Where(a => !a.Deleted)
                .Select(a => new RemsEntityAddressView(a.Id, a.AddressType.ToString(), RemsWorkspaceMapper.Address(a.Address)!))
                .ToList(),
            entity.Contacts.Where(c => !c.Deleted)
                .Select(c => new RemsEntityContactView(
                    c.Id, c.ContactRole, c.IsRequired, c.Person?.DisplayName, c.Person?.PrimaryEmail, c.Person?.MobileNumber))
                .ToList());

        return new RemsApprovalEngagementView(
            engagement.Id,
            engagement.Status.ToString(),
            engagement.Department,
            engagement.ServiceLine,
            engagement.SubServiceLine,
            engagement.SubIndustry,
            clientView,
            entityView,
            RemsWorkspaceMapper.UserRef(engagement.DepartmentDirectorId, names),
            RemsWorkspaceMapper.UserRef(engagement.EngagementExecutiveId, names),
            RemsWorkspaceMapper.UserRef(engagement.BillingManagerId, names),
            maySeeFinancials ? engagement.FirstYearFeeEstimate : null,
            maySeeFinancials ? engagement.RealizationPercentage : null,
            FinancialsRestricted: !maySeeFinancials,
            auditView,
            government is null
                ? null
                : new RemsGovernmentDetailView(
                    government.Id, government.ContractNumber, government.FloridaOnePercentStateFeeApplies,
                    government.ContractStartDate, government.ContractEndDate, government.OriginalTerm,
                    government.RenewalTerms, government.PurchaseOrderStartDate, government.PurchaseOrderEndDate),
            taxView,
            marketing,
            engagement.CommissionSplits
                .Where(s => !s.Deleted)
                .Select(s => new RemsCommissionSplitView(s.Id, RemsWorkspaceMapper.UserRef(s.EmployeeId, names)!, s.CommissionPercentage))
                .ToList());
    }

    /// <summary>
    /// Whether a role sees the first-year fee estimate and % realization. Reserved to the Department Director
    /// and the Managing Shareholder (AC-REMS-019.10) — the ONE thing an approver's role still scopes now that
    /// the rest of the engagement setup is shown to everyone on the round. Open it up by returning true.
    /// </summary>
    private static bool MaySeeFinancials(RemsApproverRole role)
        => role is RemsApproverRole.DepartmentDirector or RemsApproverRole.ManagingShareholder;

    /// <summary>
    /// Resolves option-set item ids to their labels (and, for marketing, the group tag from MetadataJson),
    /// preserving the set's sort order. Unknown ids are dropped rather than rendered as a bare guid.
    /// </summary>
    private async Task<IReadOnlyList<RemsApprovalOptionRef>> ResolveOptionRefsAsync(
        string setKey, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<RemsApprovalOptionRef>();
        }

        var set = await _optionSets.GetEffectiveSetAsync(User.GetActiveTenantId(), EntityType.Rems, setKey, cancellationToken);
        if (set is null)
        {
            return Array.Empty<RemsApprovalOptionRef>();
        }

        return set.Items
            .Where(i => !i.Deleted && ids.Contains(i.Id))
            .OrderBy(i => i.SortOrder)
            .Select(i => new RemsApprovalOptionRef(i.Id, i.Label, GroupOf(i.MetadataJson)))
            .ToList();
    }

    /// <summary>The <c>group</c> tag a marketing option carries in its metadata (null for sets without one).</summary>
    private static string? GroupOf(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            return doc.RootElement.TryGetProperty("group", out var group) ? group.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

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
