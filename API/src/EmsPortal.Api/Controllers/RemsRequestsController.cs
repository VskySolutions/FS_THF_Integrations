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
    private const string CodeDuplicateTitle = "REMS_DUPLICATE_CLIENT_TITLE";
    private const string CodeDuplicateEmail = "REMS_DUPLICATE_CLIENT_EMAIL";

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
        [FromQuery] string? type = null,
        [FromQuery] Guid? assignedAdminUserId = null,
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
            me, privileged, clientName, contact, status, type, assignedAdminUserId,
            createdFrom, createdTo, ParseScope(scope), ParsePoolFilter(poolScope), page, limit);
        var (items, total) = await _rems.ListRequestsAsync(options, cancellationToken);

        var names = await _users.GetFullNamesAsync(
            items.SelectMany(r => new[] { r.AdminAssignedToId, r.CSEId, r.CreatedById, r.UpdatedById })
                .Where(id => id.HasValue).Select(id => id!.Value),
            cancellationToken);
        var formStates = (await _rems.GetFormStatesAsync(items.Select(r => r.Id).ToList(), cancellationToken))
            .ToDictionary(f => f.RemsId);

        var rows = items.Select(r => ToRow(r, me, privileged, names, formStates));
        return Ok(ApiResponseFactory.Paginated(rows, "REMS requests retrieved.", page, limit, total));
    }

    /// <summary>
    /// How many pool requests sit in each Admin Pool view (Unassigned / Assigned to me / All), so the view
    /// switcher can show the size of each. Takes the same filters as the list and applies the same
    /// visibility rules, so a count always matches the rows that view would produce.
    /// </summary>
    [HttpGet("pool-counts")]
    [RequirePermission(Permissions.RemsPoolRead)]
    [ProducesResponseType<ApiResponse<RemsPoolCounts>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PoolCounts(
        [FromQuery] string? clientName = null,
        [FromQuery] string? contact = null,
        [FromQuery] string? status = null,
        [FromQuery] string? type = null,
        [FromQuery] Guid? assignedAdminUserId = null,
        [FromQuery] DateTime? createdFrom = null,
        [FromQuery] DateTime? createdTo = null,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } me)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        // Paging is irrelevant to an aggregate; PoolFilter is what is being counted, so neither is read.
        var options = new RemsRequestListOptions(
            me, IsPrivileged(), clientName, contact, status, type, assignedAdminUserId,
            createdFrom, createdTo, RemsListScope.Pool, RemsPoolFilter.All, 1, 1);
        var counts = await _rems.CountPoolScopesAsync(options, cancellationToken);
        return Ok(ApiResponseFactory.Success(counts, "REMS pool counts retrieved."));
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

        // Same name, same client: a request naming somebody already on file is linked to them instead of
        // being filed as new. Only when the caller did not say who — an explicit reference always wins —
        // and the type follows, so the row never reads "brand-new client" over a client we already have.
        var type = request.Type;
        if (request.ExistingClientReferenceId is null
            && await FindSoleClientByExactNameAsync(request.ClientName, cancellationToken) is { } matchedClientId)
        {
            request.ExistingClientReferenceId = matchedClientId;
            if (type == RemsRequestTypes.BrandNewClient) type = RemsRequestTypes.ExistingClient;
        }

        // Only a client we already have can have a request already. Where the reference is still null the
        // client is new to us by definition, so there is nothing for the title to collide with.
        if (await RejectDuplicateTitleAsync(request.ExistingClientReferenceId, request.Title, null, cancellationToken) is { } titleClash)
        {
            return titleClash;
        }

        if (await RejectDuplicateClientEmailAsync(
                request.ExistingClientReferenceId, request.CustomerEmail, null, cancellationToken) is { } emailClash)
        {
            return emailClash;
        }

        var submit = request.Submit;
        var rems = new REMS
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            Type = type,
            Status = submit ? RemsRequestStatuses.Submitted : RemsRequestStatuses.Draft,
            RequestedClientName = request.ClientName,
            CustomerEmail = Normalize(request.CustomerEmail),
            CustomerMobileNumber = Normalize(request.CustomerMobileNumber),
            CSEId = request.CSEId,
            ExistingClientReferenceId = request.ExistingClientReferenceId,
            AdminAssignedToId = request.AssignAdminUserId,
        };

        // Allocate the REMS number and stage the row + client person + activity + assignment + attachment
        // atomically. The person is staged first: the request's FK points at them.
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            rems.REMSNumber = await _numberGenerator.GenerateAsync(tenantId, ct);
            rems.ClientPersonId = await ResolveClientPersonAsync(rems, tenantId, ct);
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

        // Assignment can be set, changed or handed back to the pool from the edit form (AC-REMS-005). A
        // null id alone means "leave it alone", like every other field here, so returning a request to
        // the pool is said explicitly with unassignAdmin.
        var newAssignee = request.UnassignAdmin ? null : request.AssignAdminUserId;
        var assignmentChanging = (request.UnassignAdmin || request.AssignAdminUserId.HasValue)
            && rems.AdminAssignedToId != newAssignee;
        if (assignmentChanging)
        {
            // Editing a request and choosing who owns it are separate rights; holding the first must not
            // quietly grant the second.
            if (!User.HasPermission(Permissions.RemsRequestsAssign))
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponseFactory.Forbidden("Not permitted to assign this request."));
            }

            if (newAssignee is { } assignId && await _users.GetByIdAsync(assignId, cancellationToken) is null)
            {
                return BadRequest(ApiResponseFactory.Error(
                    ApiErrorCodes.ValidationFailed, "Validation failed.", "Unknown assignAdminUserId."));
            }
        }

        // Same name, same client — as on create. Confined to a request that is not already linked, so an
        // edit can never re-point an existing reference at somebody the name happens to match.
        if (rems.ExistingClientReferenceId is null
            && request.ExistingClientReferenceId is null
            && request.ClientName is not null
            && await FindSoleClientByExactNameAsync(request.ClientName, cancellationToken) is { } matchedClientId)
        {
            request.ExistingClientReferenceId = matchedClientId;
            if ((request.Type ?? rems.Type) == RemsRequestTypes.BrandNewClient)
            {
                request.Type = RemsRequestTypes.ExistingClient;
            }
        }

        // Checked against the client the request will end up with, and against the title it will end up
        // with — an edit that renames only the client still has to land somewhere free.
        var titleClient = request.ExistingClientReferenceId ?? rems.ExistingClientReferenceId ?? rems.ClientPersonId;
        if (await RejectDuplicateTitleAsync(titleClient, request.Title ?? rems.Title, rems.Id, cancellationToken) is { } titleClash)
        {
            return titleClash;
        }

        // The client this request already minted is not a duplicate of itself, so it is excluded.
        if (await RejectDuplicateClientEmailAsync(
                request.ExistingClientReferenceId ?? rems.ExistingClientReferenceId,
                request.CustomerEmail ?? rems.CustomerEmail,
                rems.ClientPersonId,
                cancellationToken) is { } emailClash)
        {
            return emailClash;
        }

        if (request.Title is not null) rems.Title = request.Title;
        if (request.Description is not null) rems.Description = request.Description;
        if (request.Type is not null) rems.Type = request.Type;
        if (request.ClientName is not null) rems.RequestedClientName = request.ClientName;
        if (request.CustomerEmail is not null) rems.CustomerEmail = Normalize(request.CustomerEmail);
        if (request.CustomerMobileNumber is not null) rems.CustomerMobileNumber = Normalize(request.CustomerMobileNumber);
        if (request.CSEId.HasValue) rems.CSEId = request.CSEId;
        if (request.ExistingClientReferenceId.HasValue) rems.ExistingClientReferenceId = request.ExistingClientReferenceId;

        // After the client fields land, so the person record follows what the request now says.
        rems.ClientPersonId = await ResolveClientPersonAsync(rems, rems.TenantId, cancellationToken);

        // Applied before the submission notice below, which stays silent on an already-owned request —
        // submitting and assigning in one save must not also ring the whole pool.
        var previousAssignee = rems.AdminAssignedToId;
        if (assignmentChanging) rems.AdminAssignedToId = newAssignee;

        // A draft can be submitted to the pool as part of an edit (draft -> submitted).
        var submittingNow = request.Submit && rems.Status == RemsRequestStatuses.Draft;
        if (submittingNow) rems.Status = RemsRequestStatuses.Submitted;

        _rems.Update(rems);
        if (submittingNow)
        {
            await _activity.WriteAsync(new CreateActivityEventDto(EntityType.Rems, rems.Id, ActivityEventTypes.RemsSubmitted), cancellationToken);
            await NotifyPoolOfSubmissionAsync(rems, me, cancellationToken);
        }
        if (assignmentChanging)
        {
            await _activity.WriteAsync(new CreateActivityEventDto(
                EntityType.Rems, rems.Id, ActivityEventTypes.RemsAssigned,
                previousAssignee?.ToString(), newAssignee?.ToString()), cancellationToken);
            // Only a new owner gets told. Handing the request back to the pool has nobody to notify —
            // NotifyPoolOfSubmissionAsync covers the pool, and only when the request enters it.
            if (newAssignee is { } assignedId)
            {
                await _notifications.DispatchAsync(AssignmentNotification(assignedId, rems), cancellationToken);
                await NotifyRequesterOfPickUpAsync(rems, assignedId, me, cancellationToken);
            }
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
            // Not the source's title: it is the same client, and a client's titles are unique.
            Title = await UniqueCopyTitleAsync(source, cancellationToken),
            Description = source.Description,
            Type = source.Type,
            Status = RemsRequestStatuses.Draft,
            RequestedClientName = source.RequestedClientName,
            CustomerEmail = source.CustomerEmail,
            CustomerMobileNumber = source.CustomerMobileNumber,
            ExistingClientReferenceId = source.ExistingClientReferenceId,
            // Same client, so the same person — a duplicate is a second request for them, not a second
            // client. Null only when the source predates the column and has not been saved since; the
            // next save of the copy resolves it.
            ClientPersonId = source.ClientPersonId,
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

        // The ambient tenant filter pins the search to the caller's active tenant. Clients only: a
        // colleague and a role contact captured off an EMS form sit in the same table, and neither is
        // somebody to open an engagement for. A name nobody matches is not an error — the caller files it
        // as a brand-new client, which is what the empty result offers them.
        var (items, _) = await _persons.ListAsync(
            term, tenantId: null, isUser: null, isActive: true, page: 1, limit: 20,
            sourceEntityType: EntityType.Client, cancellationToken: cancellationToken);
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

    /// <summary>
    /// The client already on file under this exact name, if there is exactly one. THF treats one client
    /// name as one client, so a request naming somebody we already have must reference them rather than
    /// describing a new client — otherwise the same client is onboarded twice.
    ///
    /// Matched in memory because <see cref="Person.FullName"/> is [NotMapped]: it is composed from the
    /// name columns, so there is nothing to compare against in SQL. The search that narrows the
    /// candidates is the one behind the client picker, so this can only resolve what that picker could
    /// have offered — the server never rejects a name the partner had no way to find.
    ///
    /// Two records under one name is a genuine ambiguity (two real people can share a name), so it
    /// resolves to null and the request stands as submitted: guessing which one is worse than either.
    /// </summary>
    private async Task<Guid?> FindSoleClientByExactNameAsync(string? clientName, CancellationToken cancellationToken)
    {
        var name = clientName?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length < 2)
        {
            return null;
        }

        var (candidates, _) = await _persons.ListAsync(
            name, tenantId: null, isUser: null, isActive: true, page: 1, limit: 20,
            sourceEntityType: EntityType.Client, cancellationToken: cancellationToken);
        var matches = candidates
            .Where(p => string.Equals(p.FullName.Trim(), name, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Id)
            .Distinct()
            .ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    /// <summary>
    /// Puts the request's client into the Persons table and returns who they are, so a client entered once
    /// is a record the platform holds rather than three columns on one request: the picker finds them on
    /// the next request, and a User can later be pointed at them (<c>User.PersonId</c>).
    ///
    /// Three cases, in order:
    /// <list type="bullet">
    /// <item>Intake matched a client already on file — that person IS the client. The record is THF's, not
    /// this request's, so only blank contact fields are filled; whatever is already there stands even
    /// where the request disagrees. A partner giving a different email for one referral is describing that
    /// referral, not correcting the client's master record.</item>
    /// <item>This request minted the person on an earlier save and is still the only request referring to
    /// them — it owns the record, so the edited name and contact details are written straight through.
    /// Once a second request points at them the name is no longer this request's to change, and the
    /// resolution falls through to the case below.</item>
    /// <item>Nobody on file — mint one, stamped with this request as its source.</item>
    /// </list>
    ///
    /// Runs inside the caller's unit of work: a new person is staged, not saved, so a request that fails
    /// to save leaves no client behind.
    /// </summary>
    private async Task<Guid> ResolveClientPersonAsync(REMS rems, Guid tenantId, CancellationToken cancellationToken)
    {
        var name = rems.RequestedClientName?.Trim() ?? string.Empty;
        var email = Normalize(rems.CustomerEmail);
        var phone = Normalize(rems.CustomerMobileNumber);

        // Matched an existing client. A reference that no longer resolves (person deleted, or another
        // tenant's) falls through and is treated as a client we do not have.
        if (rems.ExistingClientReferenceId is { } referenceId
            && await _persons.GetByIdAsync(referenceId, cancellationToken) is { } matched)
        {
            var filled = false;
            if (email is not null && string.IsNullOrWhiteSpace(matched.PrimaryEmail))
            {
                matched.PrimaryEmail = email;
                filled = true;
            }
            if (phone is not null && string.IsNullOrWhiteSpace(matched.MobileNumber))
            {
                matched.MobileNumber = phone;
                filled = true;
            }
            if (filled)
            {
                matched.LastProfileUpdatedOn = DateTime.UtcNow;
                _persons.Update(matched);
            }
            return matched.Id;
        }

        // A person this request minted, still referred to by nobody else. Excludes one who has since become
        // a user — their profile is theirs from that point on, not a by-product of the request.
        if (rems.ClientPersonId is { } ownedId
            && await _persons.GetByIdAsync(ownedId, cancellationToken) is { UserId: null } owned
            && owned.SourceEntityType == EntityType.Client
            && owned.SourceEntityId == rems.Id
            && !await _rems.IsClientPersonSharedAsync(ownedId, rems.Id, cancellationToken))
        {
            var (first, last) = SplitName(name);
            owned.FirstName = first;
            owned.LastName = last;
            owned.DisplayName = name;
            owned.PrimaryEmail = email;
            owned.MobileNumber = phone;
            owned.LastProfileUpdatedOn = DateTime.UtcNow;
            _persons.Update(owned);
            return owned.Id;
        }

        var (newFirst, newLast) = SplitName(name);
        var person = new Person
        {
            Id = Guid.NewGuid(),
            // Globally unique by construction (the filtered unique index on PersonCode), so no pre-check.
            PersonCode = "PER-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            // Set explicitly rather than left to ambient stamping: on create the request itself has no
            // tenant yet (it is stamped on save), so the caller's tenant is the only one that is known.
            TenantId = tenantId,
            // Client, not Rems: this person IS the client, and the picker offers only those. The id still
            // points back at the request that first named them, so the provenance pair reads "the client,
            // as captured on REMS-123".
            SourceEntityType = EntityType.Client,
            SourceEntityId = rems.Id,
            FirstName = newFirst,
            LastName = newLast,
            DisplayName = name,
            PrimaryEmail = email,
            MobileNumber = phone,
            IsActive = true,
            LastProfileUpdatedOn = DateTime.UtcNow,
        };
        await _persons.AddAsync(person, cancellationToken);
        return person.Id;
    }

    /// <summary>
    /// First word is the given name, the rest the family name. A client name is one free-text box at
    /// intake, and Person splits it in two — this is the same split the public form applies to its role
    /// contacts, so a client and a contact captured from the same name land the same way.
    /// </summary>
    private static (string First, string Last) SplitName(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return (string.Empty, string.Empty);
        }

        var space = trimmed.IndexOf(' ');
        return space < 0 ? (trimmed, string.Empty) : (trimmed[..space], trimmed[(space + 1)..].Trim());
    }

    /// <summary>
    /// The 409 for filing a brand-new client under an email another client already holds, or null to carry
    /// on. One address reaches one inbox, so a second record under it is the same client entered twice —
    /// and once there are two, neither the picker nor anybody reading a request can tell which is which.
    /// <para>
    /// Only asked of a client we are about to file as new. Naming an existing client's email on their own
    /// request is the ordinary case, not a duplicate, and the check is skipped there.
    /// </para>
    /// </summary>
    private async Task<IActionResult?> RejectDuplicateClientEmailAsync(
        Guid? existingClientReferenceId, string? email, Guid? excludingPersonId, CancellationToken cancellationToken)
    {
        var trimmed = Normalize(email);
        if (existingClientReferenceId is not null || trimmed is null)
        {
            return null;
        }

        if (await _persons.FindClientByEmailAsync(trimmed, excludingPersonId, cancellationToken) is not { } holder)
        {
            return null;
        }

        return StatusCode(StatusCodes.Status409Conflict, ApiResponseFactory.Error(
            CodeDuplicateEmail,
            "A client is already on file with that email address.",
            $"“{holder.FullName}” is already on file with the email {trimmed}. Search for them in the "
                + "Client box and pick them, rather than filing a second record for the same client."));
    }

    /// <summary>
    /// The 409 for a title this client already has, or null to carry on. A client's requests are told
    /// apart by their titles — on their history, in the pool, in every notification naming one — so two
    /// under a single title is an ambiguity nobody downstream can resolve.
    /// <para>
    /// Nothing to check when the client is not one we already have: a client with no record has no
    /// earlier request to clash with.
    /// </para>
    /// </summary>
    private async Task<IActionResult?> RejectDuplicateTitleAsync(
        Guid? clientPersonId, string? title, Guid? excludingRemsId, CancellationToken cancellationToken)
    {
        var trimmed = title?.Trim();
        if (clientPersonId is not { } clientId
            || string.IsNullOrEmpty(trimmed)
            || !await _rems.TitleExistsForClientAsync(clientId, trimmed, excludingRemsId, cancellationToken))
        {
            return null;
        }

        // The details line is what the UI puts in front of the user, so it has to stand on its own rather
        // than read as a footnote to the message above it.
        return StatusCode(StatusCodes.Status409Conflict, ApiResponseFactory.Error(
            CodeDuplicateTitle,
            "This client already has a request with that title.",
            $"This client already has a request titled “{trimmed}”. Titles are unique per client — "
                + "give this one a title that says how it differs."));
    }

    /// <summary>
    /// A title for the duplicate that this client does not already have. Copying one verbatim would clash
    /// with the request it came from, so the copy says it is one — numbered if that is taken too. Kept
    /// inside the column's 200 characters by trimming the title rather than the suffix, which is the part
    /// carrying the distinction.
    /// </summary>
    private async Task<string> UniqueCopyTitleAsync(REMS source, CancellationToken cancellationToken)
    {
        if ((source.ClientPersonId ?? source.ExistingClientReferenceId) is not { } clientId)
        {
            return source.Title;
        }

        for (var attempt = 1; attempt <= 50; attempt++)
        {
            var candidate = WithSuffix(source.Title, attempt == 1 ? " (copy)" : $" (copy {attempt})");
            if (!await _rems.TitleExistsForClientAsync(clientId, candidate, null, cancellationToken))
            {
                return candidate;
            }
        }

        // Fifty copies deep the numbering is no longer telling anybody anything; take a unique marker
        // over a title that would be rejected on the way in.
        return WithSuffix(source.Title, $" (copy {Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()})");
    }

    private static string WithSuffix(string title, string suffix)
    {
        const int max = 200;
        var stem = title.Length + suffix.Length <= max ? title : title[..(max - suffix.Length)].TrimEnd();
        return stem + suffix;
    }

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
            r.Id, r.REMSNumber, r.Title, r.RequestedClientName, r.Type, r.CreatedOnUtc, r.Status,
            r.CustomerEmail, r.CustomerMobileNumber,
            UserRefOf(r.AdminAssignedToId, names), UserRefOf(r.CSEId, names),
            form?.IndustryGroup, ems, submission,
            NameOf(names, r.CreatedById), NameOf(names, r.UpdatedById), r.UpdatedOnUtc,
            ActionsFor(r, me, privileged));
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
            rems.Type, rems.Status, rems.CustomerEmail, rems.CustomerMobileNumber,
            rems.ExistingClientReferenceId, rems.ClientPersonId,
            UserRefOf(rems.AdminAssignedToId, names), UserRefOf(rems.CSEId, names),
            form?.IndustryGroup, ems, submission, files,
            NameOf(names, rems.CreatedById), rems.CreatedOnUtc, NameOf(names, rems.UpdatedById), rems.UpdatedOnUtc,
            ActionsFor(rems, me, privileged));
    }

    /// <summary>Projects the (optional) EMS form into dashboard state strings. No form => "NotStarted"/null.</summary>
    private static (string EmsFormState, string? ClientSubmissionState) MapFormState(RemsFormStateInfo? form)
        => RemsWorkspaceMapper.FormState(form);

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
