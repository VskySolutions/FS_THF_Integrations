using EmsPortal.Api.Models;
using EmsPortal.Api.Models.Rems;
using EmsPortal.Api.Security;
using EmsPortal.Application.Abstractions.OptionSets;
using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Application.Common;
using EmsPortal.Application.Abstractions.UniversalFeatures;
using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;
using EmsPortal.Shared.Configuration;
using EmsPortal.Shared.Contracts;
using EmsPortal.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace EmsPortal.Api.Controllers;

/// <summary>
/// REMS request lifecycle backend (WO-111): the partner dashboard, Admin Pool, and the create/edit/
/// assign/delete actions on a REMS request. Endpoints are permission-gated; row visibility is
/// additionally record-level (drafts are creator-only; a partner sees requests they created or are
/// involved in; an Admin/Super Admin sees the whole tenant pool). The conversation thread, activity
/// timeline, and attachments reuse the Universal Features (Conversations/Activity/Attachments) endpoints keyed
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
    private const string CodeDuplicateEmail = "REMS_DUPLICATE_CLIENT_EMAIL";

    private readonly IRemsRepository _rems;
    private readonly IRemsEngagementRepository _engagements;
    private readonly IRemsApprovalRepository _approvals;
    private readonly IRemsDelegationRepository _delegations;
    private readonly IRemsNumberGenerator _numberGenerator;
    private readonly IUserRepository _users;
    private readonly IPersonRepository _persons;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IActivityEventWriter _activity;
    private readonly INotificationDispatcher _notifications;
    private readonly IOptionCodeResolver _codes;
    /// <summary>Where the SPA is served from — the front half of the client's public form link.</summary>
    private readonly string _baseUrl;

    public RemsRequestsController(
        IRemsRepository rems,
        IRemsEngagementRepository engagements,
        IRemsApprovalRepository approvals,
        IRemsDelegationRepository delegations,
        IRemsNumberGenerator numberGenerator,
        IUserRepository users,
        IPersonRepository persons,
        IUnitOfWork unitOfWork,
        IActivityEventWriter activity,
        INotificationDispatcher notifications,
        IOptionCodeResolver codes,
        IOptions<AppOptions> appOptions)
    {
        _rems = rems;
        _engagements = engagements;
        _approvals = approvals;
        _delegations = delegations;
        _numberGenerator = numberGenerator;
        _users = users;
        _persons = persons;
        _unitOfWork = unitOfWork;
        _activity = activity;
        _notifications = notifications;
        _codes = codes;
        _baseUrl = appOptions.Value.BaseUrl;
    }

    // -------------------- Dashboard list --------------------

    // Open to every authenticated caller, like the approvals inbox. What comes back is decided by the
    // records, not by a permission: RemsRequestListOptions carries the caller and the repository scopes
    // to what they raised or are named on, unless they are privileged (Super Admin / REMS Admin), who
    // see the tenant. A permission gate here only ever hid the page from somebody with work in it.
    [HttpGet]
    [Authorize]
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
        // "mine" or "all" (the default), the My Requests toggle. Not a permission of its own: "all" is
        // bounded by the same visibility predicate as everything else, so it widens the list only for a
        // caller who can already see past their own work. "mine" is authorship — what the caller raised,
        // or had raised for them — and drops the requests that merely name them as CSE or reviewing admin.
        [FromQuery] string? ownership = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool descending = true,
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
            createdFrom, createdTo, ParseScope(scope), ParsePoolFilter(poolScope),
            ParseOwnership(ownership), new SortRequest(sortBy, descending), page, limit);
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

    // Ungated with the list that leads here, and for the same reason. CanSee below is the real boundary:
    // a request that is not the caller's to read is a 403 whatever permissions they hold.
    [HttpGet("{id:guid}")]
    [Authorize]
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
        // An approver is the one reader the list rule cannot name: they are not the initiator, not the
        // reviewing admin and usually not the CSE — a shareholder or a commission recipient is on the
        // request because the engagement routed to them. Their notifications deep-link HERE (a REMS
        // notification carries the request id), so without this a rejected round mails four people a link
        // that 403s. Asked only after CanSee says no, so the ordinary reader still costs no query.
        if (!CanSee(rems, me, privileged)
            && !await _approvals.IsApproverOnRequestAsync(rems.Id, me, cancellationToken))
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

        // A supplied client reference must resolve to a client. Checked before the name match below, which only runs
        // when the caller named nobody and can only resolve to a client anyway — so what is checked here is
        // exactly what the caller sent.
        if (await RejectUnknownClientReferenceAsync(request.ExistingClientReferenceId, cancellationToken) is { } badClient)
        {
            return badClient;
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

        if (await RejectDuplicateClientEmailAsync(
                request.ExistingClientReferenceId, request.CustomerEmail, null, cancellationToken) is { } emailClash)
        {
            return emailClash;
        }

        // Whose request this is. A delegate acting for a shareholder produces the shareholder's work, so it
        // is stamped with both: CreatedById (set automatically on save) keeps who did it, and
        // OnBehalfOfUserId keeps whose it is. Acting as yourself leaves the latter null.
        var seat = await RemsActingAs.ResolveAsync(this, _delegations, me, cancellationToken);
        if (seat is { CanPrepare: false })
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponseFactory.Forbidden("Your delegation does not allow preparing requests."));
        }

        // Always a draft, and always UNASSIGNED. The initiator fills the whole request — client details and
        // engagement setup — and then sends the intake link to the client themselves, which is what moves
        // it on (see RemsFormController.Send). Which admin ends up reviewing it is not theirs to say: the
        // request waits in EMS Review for whichever admin picks it up.
        var rems = new REMS
        {
            Id = Guid.NewGuid(),
            Description = request.Description,
            // Both are foreign keys to their option items. A request is always one of the two types and
            // always starts as a draft, so both resolve or the list has been tampered with.
            TypeId = await _codes.RequireRemsIdAsync(RemsOptionSetKeys.Type, type, cancellationToken),
            StatusId = await _codes.RequireRemsIdAsync(
                RemsOptionSetKeys.Status, RemsRequestStatuses.Draft, cancellationToken),
            RequestedClientName = request.ClientName,
            ClientNameSuffix = Normalize(request.ClientNameSuffix),
            CustomerEmail = Normalize(request.CustomerEmail),
            CustomerMobileNumber = Normalize(request.CustomerMobileNumber),
            CSEId = request.CSEId,
            ExistingClientReferenceId = request.ExistingClientReferenceId,
            OnBehalfOfUserId = seat?.PrincipalUserId,
        };

        // Allocate the REMS number and stage the row + client person + activity + attachment atomically.
        // The person is staged first: the request's FK points at them.
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            rems.REMSNumber = await _numberGenerator.GenerateAsync(tenantId, ct);
            rems.ClientPersonId = await ResolveClientPersonAsync(rems, tenantId, ct);
            await _rems.AddAsync(rems, ct);

            // The request's one engagement, created here rather than on client submit. The initiator fills
            // the engagement setup BEFORE the client is contacted, so it has to exist from the moment the
            // request does — there is nothing to hang those fields off otherwise.
            await _engagements.AddAsync(new REMSEngagement
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                REMSId = rems.Id,
                Status = RemsEngagementStatus.Draft,
            }, ct);

            await _activity.WriteAsync(new CreateActivityEventDto(EntityType.Rems, rems.Id, ActivityEventTypes.RemsCreated), ct);
            if (request.MediaId is { } mediaId)
            {
                await _rems.AddFileAsync(new REMSFiles { Id = Guid.NewGuid(), REMSId = rems.Id, MediaId = mediaId }, ct);
            }
            // Mark the additional-entity row this came from as dealt with, so the originating request stops
            // flagging it. An unknown or already-claimed row is ignored rather than failing the create: the
            // new request is the point and it exists by here, and a second claim on the same row means two
            // people raced the same button — the first one through wins and the second is simply not
            // recorded, which is better than either failing or overwriting.
            if (request.FromAdditionalEntityId is { } sourceRowId
                && await _rems.GetAdditionalEntityAsync(sourceRowId, ct) is { CreatedREMSId: null } sourceRow)
            {
                sourceRow.CreatedREMSId = rems.Id;
                _rems.UpdateAdditionalEntity(sourceRow);
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

        // No assignment block here any more. An edit cannot re-point who reviews a request: that changes
        // hands only through PickUp / HandBack below, which is why neither this payload nor this method
        // touches AdminAssignedToId.

        // A supplied client reference must name a client — as on create, and for the same reason: an edit
        // is the other way a reference reaches the request.
        if (await RejectUnknownClientReferenceAsync(request.ExistingClientReferenceId, cancellationToken) is { } badClient)
        {
            return badClient;
        }

        // Same name, same client — as on create. Confined to a request that is not already linked, so an
        // edit can never re-point an existing reference at somebody the name happens to match.
        if (rems.ExistingClientReferenceId is null
            && request.ExistingClientReferenceId is null
            && request.ClientName is not null
            && await FindSoleClientByExactNameAsync(request.ClientName, cancellationToken) is { } matchedClientId)
        {
            request.ExistingClientReferenceId = matchedClientId;
            if ((request.Type ?? rems.Type!.Value) == RemsRequestTypes.BrandNewClient)
            {
                request.Type = RemsRequestTypes.ExistingClient;
            }
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
        if (request.Description is not null) rems.Description = request.Description;
        if (request.Type is not null)
        {
            rems.TypeId = await _codes.RequireRemsIdAsync(RemsOptionSetKeys.Type, request.Type, cancellationToken);
        }
        if (request.ClientName is not null) rems.RequestedClientName = request.ClientName;
        // Cleared by sending "" — the suffix is the one client field somebody routinely takes back off,
        // having picked "Jr." for the wrong John Smith, and an omitted field means "leave it alone" here.
        if (request.ClientNameSuffix is not null) rems.ClientNameSuffix = Normalize(request.ClientNameSuffix);
        if (request.CustomerEmail is not null) rems.CustomerEmail = Normalize(request.CustomerEmail);
        if (request.CustomerMobileNumber is not null) rems.CustomerMobileNumber = Normalize(request.CustomerMobileNumber);
        if (request.CSEId.HasValue) rems.CSEId = request.CSEId;
        if (request.ExistingClientReferenceId.HasValue) rems.ExistingClientReferenceId = request.ExistingClientReferenceId;

        // After the client fields land, so the person record follows what the request now says.
        rems.ClientPersonId = await ResolveClientPersonAsync(rems, rems.TenantId, cancellationToken);

        // Editing never moves a request along any more. A draft leaves draft only by being sent to the
        // client, which is its own action.
        _rems.Update(rems);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var refreshed = await _rems.GetByIdAsync(rems.Id, cancellationToken) ?? rems;
        var detail = await BuildDetailAsync(refreshed, me, privileged, cancellationToken);
        return Ok(ApiResponseFactory.Success(detail, "REMS request updated."));
    }

    /// <summary>
    /// Attach previously-uploaded media (POST /api/media) to a request. The create payload takes one file
    /// because that is all the intake drawer offered; the request form takes several and saves them with
    /// everything else, on a request that by then already exists — so the attaching is its own step.
    /// <para>
    /// Media already on the request is ignored rather than duplicated, so a retried save cannot file the
    /// same document twice.
    /// </para>
    /// </summary>
    [HttpPost("{id:guid}/files")]
    [RequirePermission(Permissions.RemsRequestsUpdate)]
    [ProducesResponseType<ApiResponse<RemsRequestDetail>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> AddFiles(Guid id, [FromBody] AddRemsFilesRequest request, CancellationToken cancellationToken)
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

        var existing = rems.Files.Where(f => !f.Deleted).Select(f => f.MediaId).ToHashSet();
        foreach (var mediaId in request.MediaIds.Distinct().Where(m => m != Guid.Empty && !existing.Contains(m)))
        {
            await _rems.AddFileAsync(new REMSFiles { Id = Guid.NewGuid(), REMSId = rems.Id, MediaId = mediaId }, cancellationToken);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var refreshed = await _rems.GetByIdAsync(rems.Id, cancellationToken) ?? rems;
        var detail = await BuildDetailAsync(refreshed, me, privileged, cancellationToken);
        return Ok(ApiResponseFactory.Success(detail, "REMS request attachments added."));
    }

    /// <summary>
    /// Takes one attached file off a request. The wrong document attached to a request is a document
    /// every approver then reads, so whoever may edit the request may take it off again — the same bar
    /// <see cref="AddFiles"/> applies, since attaching and detaching are the same edit in two directions.
    /// <para>
    /// The link row is soft-deleted; the stored media itself is left alone. The blob may be referenced
    /// elsewhere, and a request's history should still be able to say what was once filed under it.
    /// </para>
    /// </summary>
    [HttpDelete("{id:guid}/files/{fileId:guid}")]
    [RequirePermission(Permissions.RemsRequestsUpdate)]
    [ProducesResponseType<ApiResponse<RemsRequestDetail>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveFile(Guid id, Guid fileId, CancellationToken cancellationToken)
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

        var file = rems.Files.FirstOrDefault(f => f.Id == fileId && !f.Deleted);
        if (file is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Attachment not found on this request."));
        }

        // The DbContext converts the delete into a soft-delete (Deleted flag).
        _rems.RemoveFile(file);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var refreshed = await _rems.GetByIdAsync(rems.Id, cancellationToken) ?? rems;
        var detail = await BuildDetailAsync(refreshed, me, privileged, cancellationToken);
        return Ok(ApiResponseFactory.Success(detail, "REMS request attachment removed."));
    }

    // -------------------- Pick up / hand back --------------------

    /// <summary>
    /// The calling admin claims this request as its reviewing admin. This replaced "assign to admin": an
    /// initiator no longer names anybody, so a submitted request reaches EVERY admin's EMS Review unclaimed
    /// and the first one to press this owns it — its engagement setup, its send-back and its routing for
    /// approval (see <see cref="RemsSetupAccess"/>).
    /// <para>
    /// The caller is always the assignee, so there is no body: nobody can be handed work by somebody else.
    /// Taking one already claimed is refused rather than allowed to steal it — the holder gives it back
    /// with <see cref="HandBack"/>, and then it is anyone's again. Pressing it on a request already yours
    /// is a no-op rather than an error: two clicks on one button is not a conflict.
    /// </para>
    /// </summary>
    [HttpPost("{id:guid}/pick-up")]
    [RequirePermission(Permissions.RemsRequestsAssign)]
    [ProducesResponseType<ApiResponse<RemsRequestDetail>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PickUp(Guid id, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } me)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        if (RejectNonReviewer() is { } notReviewer)
        {
            return notReviewer;
        }

        var rems = await _rems.GetByIdAsync(id, cancellationToken);
        if (rems is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS request not found."));
        }

        // A draft has not been submitted to anybody yet — it is still its initiator's private working copy,
        // and there is nothing on it for an admin to take over.
        if (rems.Status!.Value == RemsRequestStatuses.Draft)
        {
            return Conflict(ApiResponseFactory.Error(
                ApiErrorCodes.ValidationFailed, "Cannot pick this request up.",
                "This request is still a draft — its initiator has not sent it to the client yet."));
        }

        if (rems.AdminAssignedToId is { } holder && holder != me)
        {
            var holderNames = await _users.GetFullNamesAsync(new[] { holder }, cancellationToken);
            return Conflict(ApiResponseFactory.Error(
                ApiErrorCodes.ValidationFailed, "Cannot pick this request up.",
                $"{NameOf(holderNames, holder) ?? "Another admin"} picked this request up already."));
        }

        var privileged = IsPrivileged();
        if (rems.AdminAssignedToId is null)
        {
            rems.AdminAssignedToId = me;
            _rems.Update(rems);
            await _activity.WriteAsync(new CreateActivityEventDto(
                EntityType.Rems, rems.Id, ActivityEventTypes.RemsAssigned, null, me.ToString()), cancellationToken);
            // Nobody tells the picker what they just did. The person waiting on the request is the one who
            // raised it, and until now their submission has been met with silence.
            await NotifyRequesterOfPickUpAsync(rems, me, me, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var refreshed = await _rems.GetByIdAsync(rems.Id, cancellationToken) ?? rems;
        var detail = await BuildDetailAsync(refreshed, me, privileged, cancellationToken);
        return Ok(ApiResponseFactory.Success(detail, "REMS request picked up."));
    }

    /// <summary>
    /// The holding admin returns the request to the pool, so it reads "Waiting for pickup" again and any
    /// admin may take it. The counterpart of <see cref="PickUp"/>, and the only way a request loses its
    /// reviewing admin now that saving one cannot re-point it — without this, a request taken by mistake
    /// would be stuck with whoever mis-clicked.
    /// </summary>
    [HttpPost("{id:guid}/hand-back")]
    [RequirePermission(Permissions.RemsRequestsAssign)]
    [ProducesResponseType<ApiResponse<RemsRequestDetail>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> HandBack(Guid id, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } me)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        if (RejectNonReviewer() is { } notReviewer)
        {
            return notReviewer;
        }

        var rems = await _rems.GetByIdAsync(id, cancellationToken);
        if (rems is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS request not found."));
        }

        // Yours to give back, or an elevated caller's to prise loose — the remedy when the admin holding a
        // request is away and somebody else has to work it.
        if (rems.AdminAssignedToId is { } holder && holder != me && !RemsSetupAccess.IsElevated(User))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponseFactory.Forbidden("Only the admin holding this request can hand it back."));
        }

        var privileged = IsPrivileged();
        if (rems.AdminAssignedToId is { } previous)
        {
            rems.AdminAssignedToId = null;
            _rems.Update(rems);
            await _activity.WriteAsync(new CreateActivityEventDto(
                EntityType.Rems, rems.Id, ActivityEventTypes.RemsAssigned, previous.ToString(), null), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var refreshed = await _rems.GetByIdAsync(rems.Id, cancellationToken) ?? rems;
        var detail = await BuildDetailAsync(refreshed, me, privileged, cancellationToken);
        return Ok(ApiResponseFactory.Success(detail, "REMS request handed back."));
    }

    // -------------------- The admin ↔ initiator rework loop --------------------

    /// <summary>
    /// The Admin returns a request to its initiator because the Engagement Setup needs work, with a
    /// mandatory reason. Only the setup is theirs to change afterwards — Client Intake stays read-only to
    /// them, which the engagement controller's own guards enforce.
    /// <para>
    /// Repeatable: a request can go round this loop as many times as the setup still needs work, and each
    /// pass keeps its own reason. Only one return may be open at a time, which the filtered unique index on
    /// <c>REMSSendBack</c> also enforces.
    /// </para>
    /// </summary>
    [HttpPost("{id:guid}/send-back")]
    [RequirePermission(Permissions.RemsEngagementsManage)]
    [ProducesResponseType<ApiResponse<RemsRequestDetail>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SendBack(Guid id, [FromBody] SendBackRemsRequestRequest request, CancellationToken cancellationToken)
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

        // Only from a stage the Admin actually holds. A request out with the client, already with the
        // approvers, or already returned is not theirs to send back.
        if (rems.Status!.Value is not (RemsRequestStatuses.AdminReview or RemsRequestStatuses.AwaitingAdminConfirmation))
        {
            return Conflict(ApiResponseFactory.Error(
                ApiErrorCodes.ValidationFailed, "Cannot send back.",
                "Only a request under admin review can be sent back to its initiator."));
        }

        // Who the admin is handing it to. Checked, not merely recorded: a return names whose job the rework
        // is, and both ways of getting that wrong leave it with nobody.
        //
        // "Send this to the CSE" fails on a request with no CSE named — an instruction to nobody. It also
        // fails where the INITIATOR has no REMS delegate in force: the rework is the initiator's own work,
        // and delegating is how they hand their work out. With no delegation arranged the request goes back
        // to them and only them, which is the same rule RemsSetupAccess.CanWork now applies to editing —
        // so a return the dialog allows is always a return the CSE can actually act on.
        var toCse = string.Equals(request.ReturnTo, RemsSendBackTargets.Cse, StringComparison.OrdinalIgnoreCase);
        if (toCse && rems.CSEId is null)
        {
            return BadRequest(ApiResponseFactory.Error(
                ApiErrorCodes.ValidationFailed, "Cannot send back.",
                "This request has no CSE named on it, so there is nobody to hand the rework to. Send it to the initiator instead."));
        }
        if (toCse && !await RemsSetupAccess.InitiatorHasCoverAsync(_delegations, rems, cancellationToken))
        {
            return BadRequest(ApiResponseFactory.Error(
                ApiErrorCodes.ValidationFailed, "Cannot send back.",
                "The person who raised this request has not named a REMS delegate, so their rework cannot be handed to the CSE. Send it to the initiator instead."));
        }
        var returnedTo = toCse ? rems.CSEId : rems.CreatedById;

        var reason = request.Reason.Trim();
        await _rems.AddSendBackAsync(new REMSSendBack
        {
            Id = Guid.NewGuid(),
            TenantId = rems.TenantId,
            REMSId = rems.Id,
            Reason = reason,
            ReturnedToUserId = returnedTo,
        }, cancellationToken);

        rems.StatusId = await _codes.RequireRemsIdAsync(
            RemsOptionSetKeys.Status, RemsRequestStatuses.ReturnedToInitiator, cancellationToken);
        _rems.Update(rems);
        await _activity.WriteAsync(new CreateActivityEventDto(
            EntityType.Rems, rems.Id, ActivityEventTypes.RemsSentBack, null, reason), cancellationToken);

        // Both are told either way: the one being asked, and the other so they are not working a request
        // that has moved under them. Only the wording differs, so nobody has to guess whose turn it is.
        var ownerName = returnedTo is { } owner
            ? (await _users.GetFullNamesAsync(new[] { owner }, cancellationToken))
                .TryGetValue(owner, out var name) ? name : null
            : null;
        foreach (var userId in Recipients(rems.CreatedById, rems.CSEId))
        {
            var forMe = userId == returnedTo;
            await _notifications.DispatchAsync(new CreateNotificationDto(
                userId, NotificationType.RemsRequestSubmitted,
                forMe
                    ? "A REMS request was sent back to you for engagement setup"
                    : $"A REMS request was sent back for engagement setup{(ownerName is null ? "" : $" — to {ownerName}")}",
                $"{rems.REMSNumber} — {rems.ClientDisplayName}: {reason}", EntityType.Rems, rems.Id), cancellationToken);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var refreshed = await _rems.GetByIdAsync(rems.Id, cancellationToken) ?? rems;
        var detail = await BuildDetailAsync(refreshed, me, IsPrivileged(), cancellationToken);
        return Ok(ApiResponseFactory.Success(detail, "Request sent back to its initiator."));
    }

    /// <summary>
    /// The initiator hands the revised Engagement Setup back to the Admin to confirm. Reachable from a
    /// request the Admin returned AND from one the approvers declined — both leave the setup with the
    /// initiator, and both hand back the same way. Confirming is then the Admin routing it for approval.
    /// </summary>
    [HttpPost("{id:guid}/return-to-admin")]
    [RequirePermission(Permissions.RemsRequestsUpdate)]
    [ProducesResponseType<ApiResponse<RemsRequestDetail>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReturnToAdmin(Guid id, CancellationToken cancellationToken)
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
        if (rems.Status!.Value is not (RemsRequestStatuses.ReturnedToInitiator or RemsRequestStatuses.ChangesRequested))
        {
            return Conflict(ApiResponseFactory.Error(
                ApiErrorCodes.ValidationFailed, "Cannot return to admin.",
                "This request is not currently with its initiator for rework."));
        }
        if (!CanAct(rems, me, IsPrivileged()))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponseFactory.Forbidden("Not permitted to act on this request."));
        }

        // Close the open return, if this came round the admin's loop. A round declined by the approvers
        // has no send-back row to close — its reasons live on the round's tasks.
        if (await _rems.GetOpenSendBackAsync(rems.Id, cancellationToken) is { } open)
        {
            open.ResolvedOnUtc = DateTime.UtcNow;
            _rems.UpdateSendBack(open);
        }

        rems.StatusId = await _codes.RequireRemsIdAsync(
            RemsOptionSetKeys.Status, RemsRequestStatuses.AwaitingAdminConfirmation, cancellationToken);
        _rems.Update(rems);
        await _activity.WriteAsync(new CreateActivityEventDto(EntityType.Rems, rems.Id, ActivityEventTypes.RemsReturnedToAdmin), cancellationToken);

        foreach (var userId in Recipients(rems.AdminAssignedToId))
        {
            await _notifications.DispatchAsync(new CreateNotificationDto(
                userId, NotificationType.RemsRequestPickedUp,                "A REMS engagement setup was revised",
                $"{rems.REMSNumber} — {rems.ClientDisplayName}", EntityType.Rems, rems.Id), cancellationToken);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var refreshed = await _rems.GetByIdAsync(rems.Id, cancellationToken) ?? rems;
        var detail = await BuildDetailAsync(refreshed, me, IsPrivileged(), cancellationToken);
        return Ok(ApiResponseFactory.Success(detail, "Revised setup returned to the admin."));
    }

    /// <summary>Every time this request was returned to its initiator, oldest first, with the reason given.</summary>
    [HttpGet("{id:guid}/send-backs")]
    [RequirePermission(Permissions.RemsRequestsRead)]
    [ProducesResponseType<ApiResponse<IEnumerable<RemsSendBackView>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SendBacks(Guid id, CancellationToken cancellationToken)
    {
        if (await _rems.GetByIdAsync(id, cancellationToken) is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS request not found."));
        }

        var rows = await _rems.ListSendBacksAsync(id, cancellationToken);
        // One lookup covering both who returned it and who it was handed to.
        var names = await _users.GetFullNamesAsync(
            rows.SelectMany(r => new[] { r.CreatedById, r.ReturnedToUserId })
                .Where(x => x.HasValue).Select(x => x!.Value).Distinct(),
            cancellationToken);
        var views = rows
            .Select(r => new RemsSendBackView(
                r.Id, r.Reason,
                r.CreatedById is { } by && names.TryGetValue(by, out var n) ? n : null,
                r.CreatedOnUtc, r.ResolvedOnUtc,
                r.ReturnedToUserId is { } to && names.TryGetValue(to, out var toName) ? toName : null))
            .ToList();
        return Ok(ApiResponseFactory.Success(views, "REMS send-backs retrieved."));
    }

    /// <summary>Distinct, non-empty notification recipients — the same person may hold two of these seats.</summary>
    private static IEnumerable<Guid> Recipients(params Guid?[] candidates)
        => candidates.Where(c => c.HasValue).Select(c => c!.Value).Distinct();

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
    /// Search of existing <see cref="Person"/> records (by name, email, phone) for the client picker. No
    /// external client directory exists in this platform, so <c>parentCompany</c>/<c>pastWork</c> are
    /// always null.
    /// <para>
    /// Any non-empty term searches — a minimum length would make a client whose name IS two or three
    /// characters unfindable by typing it. What bounds the work is the page limit below, not the length
    /// of the term; the picker debounces before it
    /// asks. An empty term is the one thing that searches for nothing — there is nothing to look up.
    /// </para>
    /// </summary>
    [HttpGet("/api/rems/clients/lookup")]
    [RequirePermission(Permissions.RemsRequestsCreate)]
    [ProducesResponseType<ApiResponse<IEnumerable<RemsClientLookupItem>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ClientLookup([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var term = q?.Trim() ?? string.Empty;
        if (term.Length == 0)
        {
            return Ok(ApiResponseFactory.Success(
                Array.Empty<RemsClientLookupItem>(), "Enter a name, email or phone number to search."));
        }

        // The ambient tenant filter pins the search to the caller's active tenant. Clients only: a
        // colleague and a role contact captured off an EMS form sit in the same table, and neither is
        // somebody to open an engagement for. A name nobody matches is not an error — the caller files it
        // as a brand-new client, which is what the empty result offers them.
        var (items, _) = await _persons.ListAsync(
            term, tenantId: null, isUser: null, isActive: true, SortRequest.Default, page: 1, limit: 20,
            sourceEntityType: EntityType.Client, cancellationToken: cancellationToken);
        var results = items.Select(p => new RemsClientLookupItem(p.Id, p.FullName, p.PrimaryEmail, p.MobileNumber, null, null));
        return Ok(ApiResponseFactory.Success(results, "Clients retrieved."));
    }

    /// <summary>
    /// The tenant's Admin and Super Admin users. With <paramref name="role"/> the list is instead the
    /// holders of that role in the tenant — how the CSE / Engagement Executive / Billing Manager pickers
    /// are scoped. A role nobody holds returns an empty list rather than falling back, so the caller can
    /// say the role needs somebody in it instead of silently offering people who are not.
    /// <para>
    /// This took a user GROUP name until the four seats became roles. Same shape, same one-name-in
    /// contract; only what the name refers to changed.
    /// </para>
    /// </summary>
    [HttpGet("/api/rems/admins")]
    // Gated on READING requests rather than on the assign right: what this feeds is the CSE and
    // engagement people-pickers every initiator fills in, none of whom pick anything up.
    [RequirePermission(Permissions.RemsRequestsRead)]
    [ProducesResponseType<ApiResponse<IEnumerable<RemsAdminOption>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Admins([FromQuery] string? role, CancellationToken cancellationToken)
    {
        if (User.GetActiveTenantId() is not { } tenantId)
        {
            return Ok(ApiResponseFactory.Success(Array.Empty<RemsAdminOption>(), "No active tenant."));
        }

        IReadOnlyList<User> candidates;
        if (!string.IsNullOrWhiteSpace(role))
        {
            candidates = await _users.ListByTenantRolesAsync(tenantId, new[] { role.Trim() }, cancellationToken);
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

    /// <summary>
    /// The refusal for a caller who may claim work but does not work the EMS Review queue, or null to carry
    /// on. Picking a request up means becoming the admin who reviews it, so it takes BOTH keys: the right
    /// to claim (<c>rems.requests.assign</c>, on the endpoint) and the right to do the reviewing
    /// (<c>rems.engagements.manage</c>, here).
    /// <para>
    /// The pair matters during the changeover. Partners held the assign key while naming a reviewing admin
    /// was part of intake; they lose it with that picker, but permissions travel in the JWT, so a session
    /// opened before the change is still carrying it. They have never held the second key.
    /// </para>
    /// </summary>
    private IActionResult? RejectNonReviewer()
        => User.HasPermission(Permissions.RemsEngagementsManage)
            ? null
            : StatusCode(StatusCodes.Status403Forbidden,
                ApiResponseFactory.Forbidden("Only an admin who reviews REMS requests can pick one up."));

    /// <summary>An Admin-role or Super Admin caller (sees the whole tenant; everyone else is record-scoped).</summary>
    private bool IsPrivileged() => RemsSetupAccess.IsRemsAdmin(User);

    /// <summary>
    /// Record-level VISIBILITY: privileged callers see the tenant, drafts included; everyone else sees
    /// their own drafts and the non-drafts they created or are involved in.
    /// <para>
    /// A draft is a request nobody has been asked about yet — it has no reviewing admin because none is
    /// named until one picks it up, and it is not submitted to anyone until its initiator sends the client
    /// their link. That kept it out of the admins' sight entirely, which is what changed: a referral left
    /// half-written is exactly the one an admin needs to be able to find and finish. Mirrors
    /// <c>RemsRepository.ApplyVisibility</c>, which is the same rule in SQL; the two must agree or a row
    /// appears in a list and 403s when opened.
    /// </para>
    /// <para>
    /// <c>GetById</c> admits one reader beyond this: an approver on the request (see
    /// <c>IRemsApprovalRepository.IsApproverOnRequestAsync</c>). That is deliberately NOT mirrored into the
    /// list rule, and the asymmetry runs the safe way — an extra reader who can open a request they hold a
    /// deep link to, never a row offered in a list that then refuses to open. An approver's queue is the
    /// Approval Inbox, which lists their tasks; the request lists stay what they are.
    /// </para>
    /// </summary>
    private static bool CanSee(REMS r, Guid me, bool privileged)
        => r.Status!.Value == RemsRequestStatuses.Draft
            ? privileged || IsMine(r, me)
            : privileged || IsMine(r, me) || r.AdminAssignedToId == me || r.CSEId == me;

    /// <summary>
    /// Whose request this is: the person who created it, or the principal they created it FOR. A delegate
    /// preparing a request for a shareholder produces the shareholder's work, so it has to reach the
    /// shareholder's own list — and stay reachable from the delegate's, which the first half covers.
    /// </summary>
    private static bool IsMine(REMS r, Guid me) => r.CreatedById == me || r.OnBehalfOfUserId == me;

    /// <summary>Record-level ACT (edit/delete): the creator or a privileged caller.</summary>
    private static bool CanAct(REMS r, Guid me, bool privileged)
        => privileged || IsMine(r, me);

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
            name, tenantId: null, isUser: null, isActive: true, SortRequest.Default, page: 1, limit: 20,
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
        // Two names, on purpose. The FIRST/LAST split runs on the requested name alone — "Jr." is neither a
        // given name nor a family one, and a Person filed with it stuck on the end of LastName is a Person
        // nobody finds by searching for their surname. The DISPLAY name is the one the suffix belongs to.
        var name = rems.RequestedClientName?.Trim() ?? string.Empty;
        var displayName = rems.ClientDisplayName.Trim();
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
            owned.DisplayName = displayName;
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
            DisplayName = displayName,
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

    // ResolveParentClientAsync stood alongside the reference check below — it validated the Parent Client
    // id against the request's type and returned the name to denormalise. Gone with the field
    // (DropRemsParentClient).

    /// <summary>
    /// The 400 for a client reference that does not name a client, or null to carry on. The picker offers
    /// none but persons stamped <see cref="EntityType.Client"/>, so no screen can produce this — but the
    /// reference reaches the API as a bare id, and unchecked it would link a colleague, or a role contact
    /// captured off an EMS form, as the client an engagement is opened for.
    /// <para>
    /// The gate is here rather than in <see cref="ResolveClientPersonAsync"/>, which takes the reference as
    /// settled: a reference it refused would fall through to minting a person, quietly filing a second
    /// record for a client already on file. Whatever is wrong with the id, saying so is the answer.
    /// </para>
    /// <para>
    /// Deliberately no <c>IsActive</c> check. The picker hides a deactivated client, but a request already
    /// linked to one has to stay editable — re-sending the reference it is already carrying cannot be the
    /// thing that fails the save.
    /// </para>
    /// </summary>
    private async Task<IActionResult?> RejectUnknownClientReferenceAsync(
        Guid? existingClientReferenceId, CancellationToken cancellationToken)
    {
        if (existingClientReferenceId is not { } referenceId)
        {
            return null;
        }

        // Tenant-scoped and soft-delete-filtered by the ambient query filter, so another tenant's client
        // is unknown here in exactly the way a person who does not exist is.
        if (await _persons.GetByIdAsync(referenceId, cancellationToken) is { SourceEntityType: EntityType.Client })
        {
            return null;
        }

        return BadRequest(ApiResponseFactory.Error(
            ApiErrorCodes.ValidationFailed, "Validation failed.",
            "existingClientReferenceId must name a client on file. Search for the client in the Client "
                + "box and pick them from the results."));
    }

    /// <summary>
    /// A request may still be withdrawn while it is a draft. Once the intake link has gone to the client
    /// it stays on the record — somebody outside the firm has been asked for their details by then, and
    /// the request is the only account of that.
    /// </summary>
    private static bool IsDeletable(REMS r) => r.Status!.Value == RemsRequestStatuses.Draft;

    /// <summary>
    /// Which row actions this caller may perform, combining the record-level rule with the permission.
    /// Viewing is unconditional: the row was only returned because <see cref="CanSee"/> allowed it.
    /// </summary>
    private RemsRowActions ActionsFor(REMS r, Guid me, bool privileged)
    {
        var canAct = CanAct(r, me, privileged);
        return new RemsRowActions(
            CanView: true,
            CanEdit: canAct && User.HasPermission(Permissions.RemsRequestsUpdate),
            // Not gated on CanAct, unlike everything around it: picking up is precisely the move made on
            // somebody ELSE's request, by an admin who has no standing on it yet. What bounds it is the
            // request being out of draft and unclaimed — the same pair PickUp enforces.
            CanPickUp: User.HasPermission(Permissions.RemsRequestsAssign)
                && r.Status!.Value != RemsRequestStatuses.Draft
                && r.AdminAssignedToId is null,
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
            r.Id, r.REMSNumber, r.ClientDisplayName, r.Type!.Value, r.CreatedOnUtc, r.Status!.Value,
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

        // Asked only where a CSE is actually named: with none, the send-back dialog has one answer anyway
        // and there is nothing for a delegation lookup to decide.
        var canSendBackToCse = rems.CSEId is not null
            && await RemsSetupAccess.InitiatorHasCoverAsync(_delegations, rems, cancellationToken);

        return new RemsRequestDetail(
            rems.Id, rems.REMSNumber, rems.Description, rems.ClientDisplayName,
            rems.RequestedClientName, rems.ClientNameSuffix,
            rems.Type!.Value, rems.Status!.Value, rems.CustomerEmail, rems.CustomerMobileNumber,
            rems.ExistingClientReferenceId, rems.ClientPersonId,
            UserRefOf(rems.AdminAssignedToId, names), UserRefOf(rems.CSEId, names),
            form?.IndustryGroup, ems, submission, files,
            RecordAudit.From(rems, RecordAudit.Names(names)),
            ActionsFor(rems, me, privileged),
            canSendBackToCse,
            ClientFormLink(form));
    }

    /// <summary>
    /// The client's intake link while the form is out with them, or null. One definition rather than a
    /// second copy of the window rule: the same test the Email Log applies before offering the link there.
    /// </summary>
    private string? ClientFormLink(RemsFormStateInfo? form)
        => form is not null
            && !string.IsNullOrWhiteSpace(form.InviteCode)
            && form.FormSentOnUtc is not null
            && form.FormStatus is not (RemsFormStatus.Submitted or RemsFormStatus.Cancelled)
                ? $"{_baseUrl.TrimEnd('/')}/rems/form/{form.InviteCode}"
                : null;

    /// <summary>Projects the (optional) EMS form into dashboard state strings. No form => "NotStarted"/null.</summary>
    private static (string EmsFormState, string? ClientSubmissionState) MapFormState(RemsFormStateInfo? form)
        => RemsWorkspaceMapper.FormState(form);

    // RemsRequestAssigned carries the pool broadcast: sent to every admin when a client's answers land on
    // an unclaimed request (see RemsPublicFormController). Nobody is named at intake, so there is no
    // single assignee to notify.

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

    /// <summary>
    /// The My Requests view. Absent or unrecognised means <see cref="RemsListOwnership.All"/> —
    /// everything the caller may see, bounded by the visibility predicate. Narrowing to authorship is the
    /// thing that has to be asked for, so a caller who sends nothing never silently loses rows.
    /// </summary>
    private static RemsListOwnership ParseOwnership(string? ownership) => ownership?.Trim().ToLowerInvariant() switch
    {
        "mine" => RemsListOwnership.Mine,
        _ => RemsListOwnership.All,
    };
}
