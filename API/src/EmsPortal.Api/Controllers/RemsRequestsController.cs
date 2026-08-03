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
/// REMS request lifecycle backend (WO-111): the partner dashboard, Admin Pool, and the create/edit/
/// assign/duplicate/delete actions on a REMS request. Endpoints are permission-gated; row visibility is
/// additionally record-level (drafts are creator-only; a partner sees requests they created or are
/// involved in; an Admin/Super Admin sees the whole tenant pool). The conversation thread, activity
/// timeline, and attachments reuse the Universal Features (Notes/Activity/Attachments) endpoints keyed
/// on <see cref="EntityType.Rems"/>.
/// </summary>
[ApiController]
[Route("api/rems/requests")]
[Produces("application/json")]
[Tags("REMS Requests")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status500InternalServerError)]
public sealed class RemsRequestsController : ControllerBase
{
    private const string CodeNotDeletable = "REMS_REQUEST_NOT_DELETABLE";

    private readonly IRemsRepository _rems;
    private readonly IRemsNumberGenerator _numberGenerator;
    private readonly IUserRepository _users;
    private readonly IPersonRepository _persons;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IActivityEventWriter _activity;
    private readonly INotificationDispatcher _notifications;
    private readonly IUserGroupRepository _groups;

    public RemsRequestsController(
        IRemsRepository rems,
        IRemsNumberGenerator numberGenerator,
        IUserRepository users,
        IPersonRepository persons,
        IUnitOfWork unitOfWork,
        IActivityEventWriter activity,
        INotificationDispatcher notifications,
        IUserGroupRepository groups)
    {
        _rems = rems;
        _numberGenerator = numberGenerator;
        _users = users;
        _persons = persons;
        _unitOfWork = unitOfWork;
        _activity = activity;
        _notifications = notifications;
        _groups = groups;
    }

    // -------------------- Dashboard list --------------------

    [HttpGet]
    [RequirePermission(Permissions.RemsRequestsRead)]
    [ProducesResponseType<ApiResponse<IEnumerable<RemsRequestRow>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string? clientName = null,
        [FromQuery] string? contact = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? createdFrom = null,
        [FromQuery] DateTime? createdTo = null,
        [FromQuery] string? scope = null,
        [FromQuery] string? poolScope = null,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } me)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);
        var privileged = IsPrivileged();

        var options = new RemsRequestListOptions(
            me, privileged, clientName, contact, status, createdFrom, createdTo,
            ParseScope(scope), ParsePoolFilter(poolScope), page, limit);
        var (items, total) = await _rems.ListRequestsAsync(options, cancellationToken);

        var names = await _users.GetFullNamesAsync(
            items.SelectMany(r => new[] { r.AdminAssignedToId, r.CSEId }).Where(id => id.HasValue).Select(id => id!.Value),
            cancellationToken);
        var formStates = (await _rems.GetFormStatesAsync(items.Select(r => r.Id).ToList(), cancellationToken))
            .ToDictionary(f => f.RemsId);

        var rows = items.Select(r => ToRow(r, me, privileged, names, formStates));
        return Ok(ApiResponseFactory.Paginated(rows, "REMS requests retrieved.", page, limit, total));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.RemsRequestsRead)]
    [ProducesResponseType<ApiResponse<RemsRequestDetail>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } me)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        var rems = await _rems.GetByIdAsync(id, cancellationToken);
        if (rems is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS request not found."));
        }

        var privileged = IsPrivileged();
        if (!CanSee(rems, me, privileged))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponseFactory.Forbidden("Not permitted to view this request."));
        }

        var detail = await BuildDetailAsync(rems, me, privileged, cancellationToken);
        return Ok(ApiResponseFactory.Success(detail, "REMS request retrieved."));
    }

    // -------------------- Mutations --------------------

    [HttpPost]
    [RequirePermission(Permissions.RemsRequestsCreate)]
    [ProducesResponseType<ApiResponse<RemsRequestDetail>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateRemsRequestRequest request, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } me || User.GetActiveTenantId() is not { } tenantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponseFactory.Forbidden("No active tenant/user context."));
        }

        // A supplied assignee must resolve to a user.
        if (request.AssignAdminUserId is { } assignId && await _users.GetByIdAsync(assignId, cancellationToken) is null)
        {
            return BadRequest(ApiResponseFactory.Error(
                ApiErrorCodes.ValidationFailed, "Validation failed.", "Unknown assignAdminUserId."));
        }

        var submit = request.Submit;
        var rems = new REMS
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            Type = request.Type,
            Priority = request.Priority,
            Status = submit ? RemsRequestStatuses.Submitted : RemsRequestStatuses.Draft,
            RequestedClientName = request.ClientName,
            CustomerEmail = Normalize(request.CustomerEmail),
            CustomerMobileNumber = Normalize(request.CustomerMobileNumber),
            CSEId = request.CSEId,
            ExistingClientReferenceId = request.ExistingClientReferenceId,
            AdminAssignedToId = request.AssignAdminUserId,
        };

        // Allocate the REMS number and stage the row + activity + assignment + attachment atomically.
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            rems.REMSNumber = await _numberGenerator.GenerateAsync(tenantId, ct);
            await _rems.AddAsync(rems, ct);
            await _activity.WriteAsync(new CreateActivityEventDto(EntityType.Rems, rems.Id, ActivityEventTypes.RemsCreated), ct);
            if (submit)
            {
                await _activity.WriteAsync(new CreateActivityEventDto(EntityType.Rems, rems.Id, ActivityEventTypes.RemsSubmitted), ct);
                await NotifyPoolOfSubmissionAsync(rems, me, ct);
            }
            if (request.AssignAdminUserId is { } adminId)
            {
                await _activity.WriteAsync(new CreateActivityEventDto(
                    EntityType.Rems, rems.Id, ActivityEventTypes.RemsAssigned, null, adminId.ToString()), ct);
                await _notifications.DispatchAsync(AssignmentNotification(adminId, rems), ct);
            }
            if (request.MediaId is { } mediaId)
            {
                await _rems.AddFileAsync(new REMSFiles { Id = Guid.NewGuid(), REMSId = rems.Id, MediaId = mediaId }, ct);
            }
            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

        var created = await _rems.GetByIdAsync(rems.Id, cancellationToken) ?? rems;
        var detail = await BuildDetailAsync(created, me, IsPrivileged(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponseFactory.Success(detail, "REMS request created."));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.RemsRequestsUpdate)]
    [ProducesResponseType<ApiResponse<RemsRequestDetail>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRemsRequestRequest request, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } me)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        var rems = await _rems.GetByIdAsync(id, cancellationToken);
        if (rems is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS request not found."));
        }

        var privileged = IsPrivileged();
        if (!CanAct(rems, me, privileged))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponseFactory.Forbidden("Not permitted to edit this request."));
        }

        if (request.Title is not null) rems.Title = request.Title;
        if (request.Description is not null) rems.Description = request.Description;
        if (request.Type is not null) rems.Type = request.Type;
        if (request.Priority is not null) rems.Priority = request.Priority;
        if (request.ClientName is not null) rems.RequestedClientName = request.ClientName;
        if (request.CustomerEmail is not null) rems.CustomerEmail = Normalize(request.CustomerEmail);
        if (request.CustomerMobileNumber is not null) rems.CustomerMobileNumber = Normalize(request.CustomerMobileNumber);
        if (request.CSEId.HasValue) rems.CSEId = request.CSEId;
        if (request.ExistingClientReferenceId.HasValue) rems.ExistingClientReferenceId = request.ExistingClientReferenceId;

        // A draft can be submitted to the pool as part of an edit (draft -> submitted).
        var submittingNow = request.Submit && rems.Status == RemsRequestStatuses.Draft;
        if (submittingNow) rems.Status = RemsRequestStatuses.Submitted;

        _rems.Update(rems);
        if (submittingNow)
        {
            await _activity.WriteAsync(new CreateActivityEventDto(EntityType.Rems, rems.Id, ActivityEventTypes.RemsSubmitted), cancellationToken);
            await NotifyPoolOfSubmissionAsync(rems, me, cancellationToken);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var refreshed = await _rems.GetByIdAsync(rems.Id, cancellationToken) ?? rems;
        var detail = await BuildDetailAsync(refreshed, me, privileged, cancellationToken);
        return Ok(ApiResponseFactory.Success(detail, submittingNow ? "REMS request submitted." : "REMS request updated."));
    }

    [HttpPost("{id:guid}/assign")]
    [RequirePermission(Permissions.RemsRequestsAssign)]
    [ProducesResponseType<ApiResponse<RemsRequestDetail>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignRemsRequestRequest request, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } me)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        var rems = await _rems.GetByIdAsync(id, cancellationToken);
        if (rems is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS request not found."));
        }

        var privileged = IsPrivileged();
        if (!CanAct(rems, me, privileged))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponseFactory.Forbidden("Not permitted to assign this request."));
        }

        if (await _users.GetByIdAsync(request.AdminUserId, cancellationToken) is null)
        {
            return BadRequest(ApiResponseFactory.Error(
                ApiErrorCodes.ValidationFailed, "Validation failed.", "Unknown adminUserId."));
        }

        var old = rems.AdminAssignedToId;
        if (old != request.AdminUserId)
        {
            rems.AdminAssignedToId = request.AdminUserId;
            _rems.Update(rems);
            await _activity.WriteAsync(new CreateActivityEventDto(
                EntityType.Rems, rems.Id, ActivityEventTypes.RemsAssigned, old?.ToString(), request.AdminUserId.ToString()), cancellationToken);
            await _notifications.DispatchAsync(AssignmentNotification(request.AdminUserId, rems), cancellationToken);
            await NotifyRequesterOfPickUpAsync(rems, request.AdminUserId, me, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var refreshed = await _rems.GetByIdAsync(rems.Id, cancellationToken) ?? rems;
        var detail = await BuildDetailAsync(refreshed, me, privileged, cancellationToken);
        return Ok(ApiResponseFactory.Success(detail, "REMS request assigned."));
    }

    [HttpPost("{id:guid}/duplicate")]
    [RequirePermission(Permissions.RemsRequestsCreate)]
    [ProducesResponseType<ApiResponse<RemsRequestDetail>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Duplicate(Guid id, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } me || User.GetActiveTenantId() is not { } tenantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponseFactory.Forbidden("No active tenant/user context."));
        }

        var source = await _rems.GetByIdAsync(id, cancellationToken);
        if (source is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS request not found."));
        }

        var privileged = IsPrivileged();
        if (!CanSee(source, me, privileged))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponseFactory.Forbidden("Not permitted to duplicate this request."));
        }

        // Carry forward only the intake fields; a fresh draft with no assignment/form/submission/files.
        var copy = new REMS
        {
            Id = Guid.NewGuid(),
            Title = source.Title,
            Description = source.Description,
            Type = source.Type,
            Priority = source.Priority,
            Status = RemsRequestStatuses.Draft,
            RequestedClientName = source.RequestedClientName,
            CustomerEmail = source.CustomerEmail,
            CustomerMobileNumber = source.CustomerMobileNumber,
            ExistingClientReferenceId = source.ExistingClientReferenceId,
            CSEId = source.CSEId,
        };

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            copy.REMSNumber = await _numberGenerator.GenerateAsync(tenantId, ct);
            await _rems.AddAsync(copy, ct);
            await _activity.WriteAsync(new CreateActivityEventDto(
                EntityType.Rems, copy.Id, ActivityEventTypes.RemsDuplicated, source.REMSNumber, copy.REMSNumber), ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

        var created = await _rems.GetByIdAsync(copy.Id, cancellationToken) ?? copy;
        var detail = await BuildDetailAsync(created, me, privileged, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponseFactory.Success(detail, "REMS request duplicated."));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.RemsRequestsDelete)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } me)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        var rems = await _rems.GetByIdAsync(id, cancellationToken);
        if (rems is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS request not found."));
        }

        if (!CanAct(rems, me, IsPrivileged()))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponseFactory.Forbidden("Not permitted to delete this request."));
        }

        // Hiding the action in the UI is not enough — the same window applies to a direct call.
        if (!IsDeletable(rems))
        {
            return StatusCode(StatusCodes.Status409Conflict, ApiResponseFactory.Error(
                CodeNotDeletable,
                "This request can no longer be deleted.",
                "A request can only be deleted while it is a draft, or submitted and not yet assigned to an admin."));
        }

        // The DbContext converts the delete into a soft-delete (Deleted flag).
        _rems.Remove(rems);
        await _activity.WriteAsync(new CreateActivityEventDto(EntityType.Rems, rems.Id, ActivityEventTypes.RemsDeleted), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponseFactory.Success(new { id }, "REMS request deleted."));
    }

    // -------------------- Pickers --------------------

    /// <summary>
    /// Two-or-more-character search of existing <see cref="Person"/> records (by name, email, phone) for
    /// the client picker. No external client directory exists in this platform, so
    /// <c>parentCompany</c>/<c>pastWork</c> are always null.
    /// </summary>
    [HttpGet("/api/rems/clients/lookup")]
    [RequirePermission(Permissions.RemsRequestsCreate)]
    [ProducesResponseType<ApiResponse<IEnumerable<RemsClientLookupItem>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ClientLookup([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var term = q?.Trim() ?? string.Empty;
        if (term.Length < 2)
        {
            return Ok(ApiResponseFactory.Success(
                Array.Empty<RemsClientLookupItem>(), "Enter at least two characters to search."));
        }

        // The ambient tenant filter pins the search to the caller's active tenant.
        var (items, _) = await _persons.ListAsync(term, tenantId: null, isUser: null, isActive: true, page: 1, limit: 20, cancellationToken);
        var results = items.Select(p => new RemsClientLookupItem(p.Id, p.FullName, p.PrimaryEmail, p.MobileNumber, null, null));
        return Ok(ApiResponseFactory.Success(results, "Clients retrieved."));
    }

    /// <summary>
    /// Users who can own a REMS request (the assign dropdown): Admin and Super Admin users assigned to the
    /// active tenant. With <paramref name="group"/> the list is instead the members of that user group —
    /// how the engagement's Engagement Executive / Billing Manager pickers are scoped. An unknown or empty
    /// group returns an empty list rather than falling back, so the caller can say the group needs members
    /// instead of silently offering people who are not in it.
    /// </summary>
    [HttpGet("/api/rems/admins")]
    [RequireAnyPermission(Permissions.RemsRequestsAssign, Permissions.RemsPoolRead)]
    [ProducesResponseType<ApiResponse<IEnumerable<RemsAdminOption>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Admins([FromQuery] string? group, CancellationToken cancellationToken)
    {
        if (User.GetActiveTenantId() is not { } tenantId)
        {
            return Ok(ApiResponseFactory.Success(Array.Empty<RemsAdminOption>(), "No active tenant."));
        }

        IReadOnlyList<User> candidates;
        if (!string.IsNullOrWhiteSpace(group))
        {
            var userGroup = await _groups.GetByNameAsync(group.Trim(), cancellationToken);
            if (userGroup is null)
            {
                return Ok(ApiResponseFactory.Success(Array.Empty<RemsAdminOption>(), "Group not found."));
            }

            var members = await _groups.GetMembersWithUsersByGroupAsync(userGroup.Id, cancellationToken);
            candidates = members
                .Select(m => m.User)
                .Where(u => u is { IsActive: true })
                .Select(u => u!)
                .DistinctBy(u => u.Id)
                .ToList();
        }
        else
        {
            candidates = await _users.ListByTenantRolesAsync(tenantId, new[] { Roles.Admin, Roles.SuperAdmin }, cancellationToken);
        }

        var names = await _users.GetFullNamesAsync(candidates.Select(u => u.Id), cancellationToken);
        var options = candidates
            .Select(u => new RemsAdminOption(u.Id, names.TryGetValue(u.Id, out var n) ? n : u.DisplayName, u.Email))
            .OrderBy(o => o.Name)
            .ToList();
        return Ok(ApiResponseFactory.Success(options, "Admins retrieved."));
    }

    // -------------------- Helpers --------------------

    /// <summary>An Admin-role or Super Admin caller (sees the whole tenant pool; everyone else is record-scoped).</summary>
    private bool IsPrivileged()
        => User.IsSuperAdmin() || User.GetRoles().Any(r => string.Equals(r, Roles.Admin, StringComparison.Ordinal));

    /// <summary>Record-level VISIBILITY: drafts are creator-only; non-drafts are pool-wide for privileged callers, else created-or-involved.</summary>
    private static bool CanSee(REMS r, Guid me, bool privileged)
        => r.Status == RemsRequestStatuses.Draft
            ? r.CreatedById == me
            : privileged || r.CreatedById == me || r.AdminAssignedToId == me || r.CSEId == me;

    /// <summary>Record-level ACT (edit/assign/delete): the creator or a privileged caller.</summary>
    private static bool CanAct(REMS r, Guid me, bool privileged)
        => privileged || r.CreatedById == me;

    /// <summary>A Partner-role caller. Duplicating a request is their workflow, not the pool's.</summary>
    private bool IsPartner()
        => User.GetRoles().Any(r => string.Equals(r, Roles.Partner, StringComparison.Ordinal));

    /// <summary>
    /// A request may still be withdrawn while it is a draft, or submitted but not yet picked up. Once an
    /// admin is assigned — or the customer has submitted their form — it stays on the record.
    /// </summary>
    private static bool IsDeletable(REMS r)
        => r.Status == RemsRequestStatuses.Draft
            || (r.Status == RemsRequestStatuses.Submitted && r.AdminAssignedToId is null);

    private RemsRowActions ActionsFor(REMS r, Guid me, bool privileged)
    {
        var canAct = CanAct(r, me, privileged);
        return new RemsRowActions(
            CanView: true,
            CanEdit: canAct && User.HasPermission(Permissions.RemsRequestsUpdate),
            CanAssign: canAct && User.HasPermission(Permissions.RemsRequestsAssign),
            CanDuplicate: IsPartner() && User.HasPermission(Permissions.RemsRequestsCreate),
            CanDelete: canAct && User.HasPermission(Permissions.RemsRequestsDelete) && IsDeletable(r));
    }

    private RemsRequestRow ToRow(
        REMS r, Guid me, bool privileged,
        IReadOnlyDictionary<Guid, string> names,
        IReadOnlyDictionary<Guid, RemsFormStateInfo> forms)
    {
        forms.TryGetValue(r.Id, out var form);
        var (ems, submission) = MapFormState(form);
        return new RemsRequestRow(
            r.Id, r.REMSNumber, r.Title, r.RequestedClientName, r.Type, r.Priority, r.CreatedOnUtc, r.Status,
            UserRefOf(r.AdminAssignedToId, names), UserRefOf(r.CSEId, names),
            form?.IndustryGroup, ems, submission, ActionsFor(r, me, privileged));
    }

    private async Task<RemsRequestDetail> BuildDetailAsync(REMS rems, Guid me, bool privileged, CancellationToken cancellationToken)
    {
        var names = await _users.GetFullNamesAsync(
            new[] { rems.AdminAssignedToId, rems.CSEId, rems.CreatedById, rems.UpdatedById }
                .Where(x => x.HasValue).Select(x => x!.Value),
            cancellationToken);
        var form = (await _rems.GetFormStatesAsync(new[] { rems.Id }, cancellationToken)).FirstOrDefault();
        var (ems, submission) = MapFormState(form);

        var files = rems.Files
            .Where(f => !f.Deleted)
            .Select(f => new RemsFileRef(f.Id, f.MediaId, f.Media?.OriginalFileName, f.Media?.MimeType, f.Media?.FileSize, f.Media?.PublicUrl))
            .ToList();

        return new RemsRequestDetail(
            rems.Id, rems.REMSNumber, rems.Title, rems.Description, rems.RequestedClientName,
            rems.Type, rems.Priority, rems.Status, rems.CustomerEmail, rems.CustomerMobileNumber,
            rems.ExistingClientReferenceId,
            UserRefOf(rems.AdminAssignedToId, names), UserRefOf(rems.CSEId, names),
            form?.IndustryGroup, ems, submission, files,
            NameOf(names, rems.CreatedById), rems.CreatedOnUtc, NameOf(names, rems.UpdatedById), rems.UpdatedOnUtc,
            ActionsFor(rems, me, privileged));
    }

    /// <summary>Projects the (optional) EMS form into dashboard state strings. No form => "NotStarted"/null.</summary>
    private static (string EmsFormState, string? ClientSubmissionState) MapFormState(RemsFormStateInfo? form)
    {
        if (form is null)
        {
            return ("NotStarted", null);
        }

        var ems = form.FormStatus?.ToString() ?? "NotStarted";
        var submission = form.HasSubmission || form.FormSubmittedOnUtc is not null
            ? "Submitted"
            : form.FormSentOnUtc is not null ? "AwaitingCustomer" : null;
        return (ems, submission);
    }

    private static CreateNotificationDto AssignmentNotification(Guid recipientId, REMS rems)
        => new(
            recipientId,
            NotificationType.RemsRequestAssigned,
            "A REMS request was assigned to you",
            $"{rems.REMSNumber} — {rems.Title}",
            EntityType.Rems,
            rems.Id);

    /// <summary>
    /// Tells the tenant's admins a request has landed in the pool unclaimed. Without this the pool is a
    /// screen someone has to remember to open — the whole point of submitting is that it gets picked up.
    /// The submitter is skipped (they just did it) and so is an already-assigned request.
    /// </summary>
    private async Task NotifyPoolOfSubmissionAsync(REMS rems, Guid actorId, CancellationToken cancellationToken)
    {
        if (rems.AdminAssignedToId is not null || User.GetActiveTenantId() is not { } tenantId)
        {
            return;
        }

        var admins = await _users.ListByTenantRolesAsync(tenantId, new[] { Roles.Admin, Roles.SuperAdmin }, cancellationToken);
        foreach (var admin in admins.Where(u => u.Id != actorId))
        {
            await _notifications.DispatchAsync(new CreateNotificationDto(
                admin.Id,
                NotificationType.RemsRequestSubmitted,
                "New REMS request waiting for pickup",
                $"{rems.REMSNumber} — {rems.Title}",
                EntityType.Rems,
                rems.Id), cancellationToken);
        }
    }

    /// <summary>
    /// Tells whoever raised the request that an admin now owns it. The requester (typically the Partner)
    /// otherwise gets no signal at all once they submit. Skipped when the requester is the one acting.
    /// </summary>
    private async Task NotifyRequesterOfPickUpAsync(REMS rems, Guid adminUserId, Guid actorId, CancellationToken cancellationToken)
    {
        if (rems.CreatedById is not { } requesterId || requesterId == actorId)
        {
            return;
        }

        var names = await _users.GetFullNamesAsync(new[] { adminUserId }, cancellationToken);
        var adminName = NameOf(names, adminUserId) ?? "An admin";
        await _notifications.DispatchAsync(new CreateNotificationDto(
            requesterId,
            NotificationType.RemsRequestPickedUp,
            "Your REMS request was picked up",
            $"{rems.REMSNumber} — {adminName} is now handling it.",
            EntityType.Rems,
            rems.Id), cancellationToken);
    }

    private static RemsUserRef? UserRefOf(Guid? id, IReadOnlyDictionary<Guid, string> names)
        => id is { } uid ? new RemsUserRef(uid, names.TryGetValue(uid, out var name) ? name : string.Empty) : null;

    private static string? NameOf(IReadOnlyDictionary<Guid, string> names, Guid? id)
        => id.HasValue && names.TryGetValue(id.Value, out var name) ? name : null;

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static RemsListScope ParseScope(string? scope) => scope?.Trim().ToLowerInvariant() switch
    {
        "partner" => RemsListScope.Partner,
        "pool" => RemsListScope.Pool,
        _ => RemsListScope.All,
    };

    private static RemsPoolFilter ParsePoolFilter(string? poolScope) => poolScope?.Trim().ToLowerInvariant() switch
    {
        "unassigned" => RemsPoolFilter.Unassigned,
        "mine" => RemsPoolFilter.Mine,
        _ => RemsPoolFilter.All,
    };
}
