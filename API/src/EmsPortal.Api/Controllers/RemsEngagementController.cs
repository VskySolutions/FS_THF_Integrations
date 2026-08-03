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
/// REMS engagement workspace backend (WO-114 Part A + B). Staff/Admin operations
/// (<see cref="Permissions.RemsEngagementsManage"/>) on a submitted request's engagement setup: the
/// submitted-form view, the editable client/entity/engagement workspace, the audit/government/tax
/// conditional details, cross-entity copy, and the marketing/commission steps that gate approval. Tenant
/// isolation is ambient, so a request/engagement outside the caller's tenant is simply a 404. The approval
/// workflow itself lives in <see cref="RemsApprovalController"/>.
/// </summary>
[ApiController]
[Route("api/rems")]
[Produces("application/json")]
[Tags("REMS Engagement Workspace")]
[RequirePermission(Permissions.RemsEngagementsManage)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status500InternalServerError)]
public sealed class RemsEngagementController : ControllerBase
{
    private const string CodeEngagementLocked = "REMS_ENGAGEMENT_LOCKED";
    private const string CodeCopyInvalid = "REMS_COPY_INVALID";

    private const string MarketingSetKey = "REMSMarketing_MarketingMethods.MarketingMethodId";
    private const string TaxFormSetKey = "REMS.TaxForm";

    private readonly IRemsRepository _rems;
    private readonly IRemsFormRepository _forms;
    private readonly IRemsClientRepository _clients;
    private readonly IRemsEngagementRepository _engagements;
    private readonly IRemsSettingsRepository _settings;
    private readonly IAddressRepository _addresses;
    private readonly IPersonRepository _persons;
    private readonly IMediaRepository _media;
    private readonly IOptionSetRepository _optionSets;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IActivityEventWriter _activity;

    public RemsEngagementController(
        IRemsRepository rems,
        IRemsFormRepository forms,
        IRemsClientRepository clients,
        IRemsEngagementRepository engagements,
        IRemsSettingsRepository settings,
        IAddressRepository addresses,
        IPersonRepository persons,
        IMediaRepository media,
        IOptionSetRepository optionSets,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IActivityEventWriter activity)
    {
        _rems = rems;
        _forms = forms;
        _clients = clients;
        _engagements = engagements;
        _settings = settings;
        _addresses = addresses;
        _persons = persons;
        _media = media;
        _optionSets = optionSets;
        _users = users;
        _unitOfWork = unitOfWork;
        _activity = activity;
    }

    // -------------------- Part A: submitted-form view + workspace read --------------------

    /// <summary>
    /// The client-forms list (AC-REMS-013.1): every request that has an EMS form, indicating
    /// submitted/not-submitted, client name, submission date and the assigned Admin/CSE.
    /// </summary>
    [HttpGet("client-forms")]
    [ProducesResponseType<ApiResponse<IEnumerable<RemsClientFormRow>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ClientForms(
        [FromQuery] int page = 1, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);

        var (items, total) = await _forms.ListClientFormsAsync(page, limit, cancellationToken);
        var names = await _users.GetFullNamesAsync(
            items.SelectMany(i => new[] { i.AdminAssignedToId, i.CSEId }).Where(id => id.HasValue).Select(id => id!.Value),
            cancellationToken);

        var rows = items.Select(i => new RemsClientFormRow(
            i.RemsId, i.RemsNumber, i.ClientName, i.RequestStatus, HasForm: true, i.Submitted, i.SubmittedOnUtc,
            RemsWorkspaceMapper.UserRef(i.AdminAssignedToId, names), RemsWorkspaceMapper.UserRef(i.CSEId, names)));

        return Ok(ApiResponseFactory.Paginated(rows, "REMS client forms retrieved.", page, limit, total));
    }

    /// <summary>
    /// The read-only submitted-form view (AC-REMS-013.2/3), rendered from the immutable
    /// <c>REMSFormSubmission</c> payload as plain fields — distinct from the editable workspace data.
    /// </summary>
    [HttpGet("requests/{remsId:guid}/submission")]
    [ProducesResponseType<ApiResponse<RemsSubmissionView>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Submission(Guid remsId, CancellationToken cancellationToken)
    {
        var rems = await _rems.GetByIdAsync(remsId, cancellationToken);
        if (rems is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS request not found."));
        }

        var form = await _forms.GetWithSubmissionsByRemsIdAsync(remsId, cancellationToken);
        var submission = form?.Submissions.OrderByDescending(s => s.SubmittedOnUtc).FirstOrDefault();
        if (form is null || submission is null)
        {
            return NotFound(ApiResponseFactory.NotFound("This request has no submitted form."));
        }

        var payload = RemsFormPayloadJson.TryDeserialize(submission.SubmittedPayload) ?? new RemsFormPayloadV1();
        var view = new RemsSubmissionView(
            submission.Id, rems.Id, rems.REMSNumber, form.IndustryGroup, rems.CustomerEmail, submission.SubmittedOnUtc, payload);
        return Ok(ApiResponseFactory.Success(view, "REMS submitted form retrieved."));
    }

    /// <summary>
    /// The engagement workspace (AC-REMS-014): the client, all its entities, and each entity's engagement
    /// with its audit/government/tax detail, addresses and contacts.
    /// </summary>
    [HttpGet("requests/{remsId:guid}/engagement")]
    [ProducesResponseType<ApiResponse<RemsEngagementWorkspace>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Workspace(Guid remsId, CancellationToken cancellationToken)
    {
        var rems = await _rems.GetByIdAsync(remsId, cancellationToken);
        if (rems is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS request not found."));
        }

        var client = await _clients.GetByRemsIdAsync(remsId, cancellationToken);
        if (client is null)
        {
            return NotFound(ApiResponseFactory.NotFound("This request has no client yet; the form has not been submitted."));
        }

        var entityIds = client.Entities.Where(e => !e.Deleted).Select(e => e.Id).ToList();
        var engagements = await _engagements.ListByEntityIdsAsync(entityIds, cancellationToken);
        var engagementIds = engagements.Select(e => e.Id).ToList();

        var audit = (await _engagements.ListAuditDetailsAsync(engagementIds, cancellationToken)).ToDictionary(d => d.REMSEngagementId);
        var government = (await _engagements.ListGovernmentDetailsAsync(engagementIds, cancellationToken)).ToDictionary(d => d.REMSEngagementId);
        var tax = (await _engagements.ListTaxDetailsAsync(engagementIds, cancellationToken)).ToDictionary(d => d.REMSEngagementId);
        var engagementsByEntity = engagements.ToDictionary(e => e.REMSEntityId);

        // The department → director map travels with the workspace so the setup form can name the director
        // a department maps to as soon as it is picked, instead of waiting for the save to come back.
        var settings = await _settings.GetAsync(cancellationToken);
        var directorRows = settings?.DepartmentDirectors.Where(d => !d.Deleted).ToList() ?? new();

        var names = await _users.GetFullNamesAsync(
            CollectUserIds(engagements).Concat(directorRows.Select(d => d.DirectorUserId)), cancellationToken);

        var departmentDirectors = directorRows
            .OrderBy(d => d.Department)
            .Select(d => new RemsDepartmentDirectorView(
                d.Department,
                new RemsUserRef(d.DirectorUserId, names.TryGetValue(d.DirectorUserId, out var n) ? n : string.Empty)))
            .ToList();

        var workspace = RemsWorkspaceMapper.Workspace(
            rems, client, engagementsByEntity, audit, government, tax, names, departmentDirectors);
        return Ok(ApiResponseFactory.Success(workspace, "REMS engagement workspace retrieved."));
    }

    // -------------------- Part A: client + entity editing --------------------

    /// <summary>Update the client record (AC-REMS-014). The client email is locked and never changes.</summary>
    [HttpPut("requests/{remsId:guid}/client")]
    [ProducesResponseType<ApiResponse<RemsClientView>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateClient(Guid remsId, [FromBody] UpdateRemsClientRequest request, CancellationToken cancellationToken)
    {
        var client = await _clients.GetByRemsIdAsync(remsId, cancellationToken);
        if (client is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS client not found."));
        }

        if (request.Name is not null) client.Name = request.Name.Trim();
        if (request.MobileNumber is not null) client.MobileNumber = Normalize(request.MobileNumber);
        if (request.ReferralSource is not null) client.ReferralSource = Normalize(request.ReferralSource);
        if (request.BillingContactName is not null) client.BillingContactName = Normalize(request.BillingContactName);
        if (request.BillingEmail is not null) client.BillingEmail = Normalize(request.BillingEmail);

        if (request.BillingAddress is { } billing)
        {
            client.BillingAddressId = await UpsertAddressAsync(client.BillingAddressId, billing, AddressType.Billing, cancellationToken);
        }

        _clients.Update(client);
        await _activity.WriteAsync(new CreateActivityEventDto(EntityType.Rems, remsId, ActivityEventTypes.RemsEngagementUpdated), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var refreshed = await _clients.GetByRemsIdAsync(remsId, cancellationToken) ?? client;
        var view = new RemsClientView(
            refreshed.Id, refreshed.Name, refreshed.Email, refreshed.MobileNumber, refreshed.ReferralSource,
            refreshed.BillingContactName, refreshed.BillingEmail, RemsWorkspaceMapper.Address(refreshed.BillingAddress));
        return Ok(ApiResponseFactory.Success(view, "REMS client updated."));
    }

    /// <summary>Replace an entity's physical/mailing addresses (AC-REMS-014). Each null =&gt; remove that type.</summary>
    [HttpPut("entities/{entityId:guid}/addresses")]
    [ProducesResponseType<ApiResponse<IEnumerable<RemsEntityAddressView>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateEntityAddresses(
        Guid entityId, [FromBody] UpdateRemsEntityAddressesRequest request, CancellationToken cancellationToken)
    {
        var entity = await _clients.GetEntityAsync(entityId, cancellationToken);
        if (entity is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS entity not found."));
        }

        await UpsertEntityAddressAsync(entity, RemsAddressType.Physical, request.PhysicalAddress, cancellationToken);
        await UpsertEntityAddressAsync(entity, RemsAddressType.Mailing, request.MailingAddress, cancellationToken);

        await _activity.WriteAsync(new CreateActivityEventDto(
            EntityType.Rems, entity.Client!.REMSId, ActivityEventTypes.RemsEngagementUpdated), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var refreshed = await _clients.GetEntityAsync(entityId, cancellationToken) ?? entity;
        var rows = refreshed.Addresses.Where(a => !a.Deleted)
            .Select(a => new RemsEntityAddressView(a.Id, a.AddressType.ToString(), RemsWorkspaceMapper.Address(a.Address)!));
        return Ok(ApiResponseFactory.Success(rows, "REMS entity addresses updated."));
    }

    /// <summary>Replace an entity's contacts (AC-REMS-014). Each contact is upserted by its role; absent roles are removed.</summary>
    [HttpPut("entities/{entityId:guid}/contacts")]
    [ProducesResponseType<ApiResponse<IEnumerable<RemsEntityContactView>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateEntityContacts(
        Guid entityId, [FromBody] UpdateRemsEntityContactsRequest request, CancellationToken cancellationToken)
    {
        var entity = await _clients.GetEntityAsync(entityId, cancellationToken);
        if (entity is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS entity not found."));
        }

        var existing = entity.Contacts.Where(c => !c.Deleted).ToList();
        var desiredRoles = request.Contacts.Select(c => c.Role.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Remove contacts whose role is no longer present.
        foreach (var contact in existing.Where(c => !desiredRoles.Contains(c.ContactRole)))
        {
            _clients.RemoveEntityContact(contact);
        }

        // Upsert each supplied contact by role.
        foreach (var input in request.Contacts)
        {
            var role = input.Role.Trim();
            var contact = existing.FirstOrDefault(c => string.Equals(c.ContactRole, role, StringComparison.OrdinalIgnoreCase));
            if (contact is null)
            {
                var person = NewContactPerson(input);
                await _persons.AddAsync(person, cancellationToken);
                await _clients.AddEntityContactAsync(new REMSEntityContact
                {
                    Id = Guid.NewGuid(),
                    REMSEntityId = entity.Id,
                    PersonId = person.Id,
                    ContactRole = role,
                    IsRequired = input.IsRequired,
                }, cancellationToken);
            }
            else
            {
                contact.IsRequired = input.IsRequired;
                if (contact.Person is { } person)
                {
                    ApplyContactPerson(person, input);
                    _persons.Update(person);
                }
            }
        }

        await _activity.WriteAsync(new CreateActivityEventDto(
            EntityType.Rems, entity.Client!.REMSId, ActivityEventTypes.RemsEngagementUpdated), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var refreshed = await _clients.GetEntityAsync(entityId, cancellationToken) ?? entity;
        var rows = refreshed.Contacts.Where(c => !c.Deleted)
            .Select(c => new RemsEntityContactView(c.Id, c.ContactRole, c.IsRequired, c.Person?.DisplayName, c.Person?.PrimaryEmail, c.Person?.MobileNumber));
        return Ok(ApiResponseFactory.Success(rows, "REMS entity contacts updated."));
    }

    // -------------------- Part A: engagement editing --------------------

    /// <summary>
    /// Update an engagement's team, service placement and fee/realization (AC-REMS-014). Setting the
    /// department prefills the mapped director unless one is supplied. Returns the engagement plus the
    /// director the chosen department maps to (prefill hint).
    /// </summary>
    [HttpPut("engagements/{id:guid}")]
    [ProducesResponseType<ApiResponse<RemsEngagementUpdateResult>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateEngagement(Guid id, [FromBody] UpdateRemsEngagementRequest request, CancellationToken cancellationToken)
    {
        var engagement = await _engagements.GetWithContextAsync(id, cancellationToken);
        if (engagement is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS engagement not found."));
        }
        if (!IsEditable(engagement))
        {
            return EngagementLocked();
        }

        // Any supplied team member must resolve to a real user.
        foreach (var userId in new[] { request.DepartmentDirectorId, request.EngagementExecutiveId, request.BillingManagerId })
        {
            if (userId is { } uid && await _users.GetByIdAsync(uid, cancellationToken) is null)
            {
                return BadRequest(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed, "Validation failed.", $"Unknown user id {uid}."));
            }
        }

        // Compare like with like: the stored department is normalized, the incoming one is not yet.
        var departmentChanged = request.Department is not null
            && !string.Equals(Normalize(request.Department), engagement.Department, StringComparison.Ordinal);
        if (request.Department is not null) engagement.Department = Normalize(request.Department);
        if (request.ServiceLine is not null) engagement.ServiceLine = Normalize(request.ServiceLine);

        var mappedDirector = await MappedDirectorAsync(engagement.Department, cancellationToken);
        if (request.DepartmentDirectorId.HasValue)
        {
            engagement.DepartmentDirectorId = request.DepartmentDirectorId;
        }
        else if (departmentChanged || engagement.DepartmentDirectorId is null)
        {
            // Prefill from the tenant department-director map (may be null = unassigned placeholder). Also
            // fills an engagement that is still unassigned — the department may have gained a director
            // (a department head) only after this engagement was set up, and re-picking the same
            // department would otherwise never pick it up.
            engagement.DepartmentDirectorId = mappedDirector;
        }

        if (request.EngagementExecutiveId.HasValue) engagement.EngagementExecutiveId = request.EngagementExecutiveId;
        if (request.BillingManagerId.HasValue) engagement.BillingManagerId = request.BillingManagerId;
        if (request.FirstYearFeeEstimate.HasValue) engagement.FirstYearFeeEstimate = request.FirstYearFeeEstimate;
        if (request.RealizationPercentage.HasValue) engagement.RealizationPercentage = request.RealizationPercentage;

        _engagements.Update(engagement);
        await LogEngagementUpdatedAsync(engagement, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var view = await BuildEngagementViewAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success(new RemsEngagementUpdateResult(view!, mappedDirector), "REMS engagement updated."));
    }

    /// <summary>
    /// Link a previously-uploaded media id as the audit engagement's signed client-acceptance form
    /// (AC-REMS-014.12). The audit detail is created on first link.
    /// </summary>
    [HttpPost("engagements/{id:guid}/audit/client-acceptance-form")]
    [ProducesResponseType<ApiResponse<RemsEngagementView>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> LinkClientAcceptanceForm(Guid id, [FromBody] LinkClientAcceptanceFormRequest request, CancellationToken cancellationToken)
    {
        var engagement = await _engagements.GetWithContextAsync(id, cancellationToken);
        if (engagement is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS engagement not found."));
        }
        if (!IsEditable(engagement))
        {
            return EngagementLocked();
        }
        if (await _media.GetByIdAsync(request.MediaId, cancellationToken) is null)
        {
            return BadRequest(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed, "Validation failed.", "Unknown mediaId."));
        }

        var detail = await _engagements.GetAuditDetailAsync(id, cancellationToken);
        if (detail is null)
        {
            await _engagements.AddAuditDetailAsync(new REMSEngagementAuditDetail
            {
                Id = Guid.NewGuid(),
                REMSEngagementId = id,
                ClientAcceptanceFormMediaId = request.MediaId,
            }, cancellationToken);
        }
        else
        {
            detail.ClientAcceptanceFormMediaId = request.MediaId;
        }

        await LogEngagementUpdatedAsync(engagement, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var view = await BuildEngagementViewAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success(view!, "Client acceptance form linked."));
    }

    /// <summary>Set the government-audit contract detail: contract number + Florida 1% flag and contract/PO dates (AC-REMS-014.13).</summary>
    [HttpPut("engagements/{id:guid}/government")]
    [ProducesResponseType<ApiResponse<RemsEngagementView>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateGovernmentDetail(Guid id, [FromBody] UpdateRemsGovernmentDetailRequest request, CancellationToken cancellationToken)
    {
        var engagement = await _engagements.GetWithContextAsync(id, cancellationToken);
        if (engagement is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS engagement not found."));
        }
        if (!IsEditable(engagement))
        {
            return EngagementLocked();
        }

        var detail = await _engagements.GetGovernmentDetailAsync(id, cancellationToken);
        if (detail is null)
        {
            detail = new REMSEngagementGovernmentDetail { Id = Guid.NewGuid(), REMSEngagementId = id };
            await _engagements.AddGovernmentDetailAsync(detail, cancellationToken);
        }

        detail.ContractNumber = Normalize(request.ContractNumber);
        detail.FloridaOnePercentStateFeeApplies = request.FloridaOnePercentStateFeeApplies;
        detail.ContractStartDate = request.ContractStartDate;
        detail.ContractEndDate = request.ContractEndDate;
        detail.OriginalTerm = Normalize(request.OriginalTerm);
        detail.RenewalTerms = Normalize(request.RenewalTerms);
        detail.PurchaseOrderStartDate = request.PurchaseOrderStartDate;
        detail.PurchaseOrderEndDate = request.PurchaseOrderEndDate;

        await LogEngagementUpdatedAsync(engagement, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var view = await BuildEngagementViewAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success(view!, "Government audit detail updated."));
    }

    /// <summary>
    /// Set the tax engagement detail (AC-REMS-014.14): the fiscal year end (which recomputes the due-date
    /// schedule) and the tax-form checklist. Requires a Tax engagement.
    /// </summary>
    [HttpPut("engagements/{id:guid}/tax")]
    [ProducesResponseType<ApiResponse<RemsEngagementView>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateTaxDetail(Guid id, [FromBody] UpdateRemsTaxDetailRequest request, CancellationToken cancellationToken)
    {
        var engagement = await _engagements.GetWithContextAsync(id, cancellationToken);
        if (engagement is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS engagement not found."));
        }
        if (!IsEditable(engagement))
        {
            return EngagementLocked();
        }

        // Validate the tax-form ids against the REMS.TaxForm option set.
        if (request.TaxFormIds.Count > 0)
        {
            var valid = await ResolveOptionItemIdsAsync(TaxFormSetKey, cancellationToken);
            var unknown = request.TaxFormIds.Where(f => !valid.Contains(f)).ToList();
            if (unknown.Count > 0)
            {
                return BadRequest(ApiResponseFactory.Error(
                    ApiErrorCodes.ValidationFailed, "Validation failed.", "One or more taxFormIds are not valid REMS tax forms."));
            }
        }

        var detail = await _engagements.GetTaxDetailAsync(id, cancellationToken);
        if (detail is null)
        {
            detail = new REMSEngagementTaxDetail { Id = Guid.NewGuid(), REMSEngagementId = id };
            await _engagements.AddTaxDetailAsync(detail, cancellationToken);
        }

        detail.FiscalYearEnd = request.FiscalYearEnd;
        detail.CalculatedDueDates = request.FiscalYearEnd is { } fye ? RemsTaxDueDates.ComputeJson(fye) : null;

        // Reconcile the tax-form checklist rows by TaxFormId.
        var existingForms = detail.TaxForms.Where(f => !f.Deleted).ToList();
        var desiredForms = request.TaxFormIds.Distinct().ToList();
        foreach (var form in existingForms.Where(f => !desiredForms.Contains(f.TaxFormId)))
        {
            _engagements.RemoveTaxForm(form);
        }
        foreach (var formId in desiredForms.Where(f => existingForms.All(x => x.TaxFormId != f)))
        {
            await _engagements.AddTaxFormAsync(new REMSEngagementTaxForm
            {
                Id = Guid.NewGuid(),
                REMSEngagementTaxDetailId = detail.Id,
                TaxFormId = formId,
            }, cancellationToken);
        }

        await LogEngagementUpdatedAsync(engagement, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var view = await BuildEngagementViewAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success(view!, "Tax engagement detail updated."));
    }

    // -------------------- Part B: copy, marketing, commission --------------------

    /// <summary>
    /// One-time copy of another entity's engagement setup into this one (AC-REMS-015): copies EXACTLY the
    /// address, department, service line, engagement executive and billing manager — never fee/realization,
    /// tax/audit/government, marketing or approval. Rejects copy-from-self and when no other entity exists.
    /// </summary>
    [HttpPost("engagements/{id:guid}/copy-from/{sourceId:guid}")]
    [ProducesResponseType<ApiResponse<RemsEngagementView>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CopyFrom(Guid id, Guid sourceId, CancellationToken cancellationToken)
    {
        if (id == sourceId)
        {
            return CopyInvalid("An engagement cannot be copied from itself.");
        }

        var target = await _engagements.GetWithContextAsync(id, cancellationToken);
        if (target is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS engagement not found."));
        }
        if (!IsEditable(target))
        {
            return EngagementLocked();
        }

        // A copy source must exist under the SAME client, and the client must have another entity at all.
        var otherEntities = target.Entity!.Client!.Entities.Count(e => !e.Deleted && e.Id != target.REMSEntityId);
        if (otherEntities == 0)
        {
            return CopyInvalid("There is no other entity to copy from.");
        }

        var source = await _engagements.GetWithContextAsync(sourceId, cancellationToken);
        if (source is null || source.Entity!.REMSClientId != target.Entity!.REMSClientId)
        {
            return CopyInvalid("The source engagement must belong to another entity of the same client.");
        }

        // Copy EXACTLY the five fields (015.3); nothing else (015.4).
        target.Department = source.Department;
        target.ServiceLine = source.ServiceLine;
        target.EngagementExecutiveId = source.EngagementExecutiveId;
        target.BillingManagerId = source.BillingManagerId;
        _engagements.Update(target);

        // Copy the source entity's addresses onto the target entity (upsert by type, cloning the Address rows).
        var targetEntity = await _clients.GetEntityAsync(target.REMSEntityId, cancellationToken);
        foreach (var sourceAddress in source.Entity!.Addresses.Where(a => !a.Deleted))
        {
            if (sourceAddress.Address is null)
            {
                continue;
            }
            await UpsertEntityAddressFromAsync(targetEntity!, sourceAddress.AddressType, sourceAddress.Address, cancellationToken);
        }

        await LogEngagementUpdatedAsync(target, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var view = await BuildEngagementViewAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success(view!, "REMS engagement copied."));
    }

    /// <summary>
    /// Set the engagement marketing tags (AC-REMS-017): a list of REMS marketing option ids, at least one
    /// required to save. Saving makes the approval step reachable.
    /// </summary>
    [HttpPut("engagements/{id:guid}/marketing")]
    [ProducesResponseType<ApiResponse<RemsEngagementView>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetMarketing(Guid id, [FromBody] SetRemsMarketingRequest request, CancellationToken cancellationToken)
    {
        var engagement = await _engagements.GetWithContextAsync(id, cancellationToken);
        if (engagement is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS engagement not found."));
        }
        if (!IsEditable(engagement))
        {
            return EngagementLocked();
        }

        var valid = await ResolveOptionItemIdsAsync(MarketingSetKey, cancellationToken);
        var unknown = request.MarketingMethodIds.Where(m => !valid.Contains(m)).ToList();
        if (unknown.Count > 0)
        {
            return BadRequest(ApiResponseFactory.Error(
                ApiErrorCodes.ValidationFailed, "Validation failed.", "One or more marketingMethodIds are not valid REMS marketing methods."));
        }

        var desired = request.MarketingMethodIds.Distinct().ToList();
        var existing = engagement.MarketingMethods.Where(m => !m.Deleted).ToList();
        foreach (var method in existing.Where(m => !desired.Contains(m.MarketingMethodId)))
        {
            _engagements.RemoveMarketingMethod(method);
        }
        foreach (var methodId in desired.Where(m => existing.All(x => x.MarketingMethodId != m)))
        {
            await _engagements.AddMarketingMethodAsync(new REMSEngagementMarketingMethod
            {
                Id = Guid.NewGuid(),
                REMSEngagementId = id,
                MarketingMethodId = methodId,
            }, cancellationToken);
        }

        await LogEngagementUpdatedAsync(engagement, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var view = await BuildEngagementViewAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success(view!, "REMS marketing tags updated."));
    }

    /// <summary>
    /// Set the engagement commission splits (AC-REMS-016): up to ten recipients, each &gt; 0 and &lt;= 100.
    /// Recipients become required approvers. Removal is allowed only before approval is sent (enforced by the
    /// editable guard).
    /// </summary>
    [HttpPut("engagements/{id:guid}/commission")]
    [ProducesResponseType<ApiResponse<RemsEngagementView>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetCommission(Guid id, [FromBody] SetRemsCommissionRequest request, CancellationToken cancellationToken)
    {
        var engagement = await _engagements.GetWithContextAsync(id, cancellationToken);
        if (engagement is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS engagement not found."));
        }
        if (!IsEditable(engagement))
        {
            return EngagementLocked();
        }

        // Every recipient must resolve to a real user.
        foreach (var split in request.Splits)
        {
            if (await _users.GetByIdAsync(split.EmployeeId, cancellationToken) is null)
            {
                return BadRequest(ApiResponseFactory.Error(
                    ApiErrorCodes.ValidationFailed, "Validation failed.", $"Unknown employeeId {split.EmployeeId}."));
            }
        }

        var existing = engagement.CommissionSplits.Where(s => !s.Deleted).ToList();
        var desiredByEmployee = request.Splits.ToDictionary(s => s.EmployeeId, s => s.Percentage);

        foreach (var split in existing.Where(s => !desiredByEmployee.ContainsKey(s.EmployeeId)))
        {
            _engagements.RemoveCommissionSplit(split);
        }
        foreach (var (employeeId, percentage) in desiredByEmployee)
        {
            var split = existing.FirstOrDefault(s => s.EmployeeId == employeeId);
            if (split is null)
            {
                await _engagements.AddCommissionSplitAsync(new REMSEngagementCommissionSplit
                {
                    Id = Guid.NewGuid(),
                    REMSEngagementId = id,
                    EmployeeId = employeeId,
                    CommissionPercentage = percentage,
                }, cancellationToken);
            }
            else if (split.CommissionPercentage != percentage)
            {
                split.CommissionPercentage = percentage;
            }
        }

        await LogEngagementUpdatedAsync(engagement, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var view = await BuildEngagementViewAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success(view!, "REMS commission splits updated."));
    }

    // -------------------- Helpers --------------------

    /// <summary>An engagement is editable only while it is Draft or has been Rejected (a fresh rework); locked once routed for approval or approved.</summary>
    private static bool IsEditable(REMSEngagement engagement)
        => engagement.Status is RemsEngagementStatus.Draft or RemsEngagementStatus.Rejected;

    private async Task<Guid?> MappedDirectorAsync(string? department, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(department))
        {
            return null;
        }

        var settings = await _settings.GetAsync(cancellationToken);
        var normalized = department.Trim().ToLowerInvariant();
        return settings?.DepartmentDirectors
            .Where(d => !d.Deleted)
            .FirstOrDefault(d => d.Department.Trim().ToLowerInvariant() == normalized)?.DirectorUserId;
    }

    private Task LogEngagementUpdatedAsync(REMSEngagement engagement, CancellationToken cancellationToken)
        => _activity.WriteAsync(
            new CreateActivityEventDto(EntityType.Rems, engagement.Entity!.Client!.REMSId, ActivityEventTypes.RemsEngagementUpdated),
            cancellationToken);

    private async Task<RemsEngagementView?> BuildEngagementViewAsync(Guid engagementId, CancellationToken cancellationToken)
    {
        var engagement = await _engagements.GetByIdAsync(engagementId, cancellationToken);
        if (engagement is null)
        {
            return null;
        }

        var audit = await _engagements.GetAuditDetailAsync(engagementId, cancellationToken);
        var government = await _engagements.GetGovernmentDetailAsync(engagementId, cancellationToken);
        var tax = await _engagements.GetTaxDetailAsync(engagementId, cancellationToken);
        var names = await _users.GetFullNamesAsync(CollectUserIds(new[] { engagement }), cancellationToken);
        return RemsWorkspaceMapper.Engagement(engagement, audit, government, tax, names);
    }

    private static IReadOnlyCollection<Guid> CollectUserIds(IEnumerable<REMSEngagement> engagements)
    {
        var ids = new HashSet<Guid>();
        foreach (var e in engagements)
        {
            if (e.DepartmentDirectorId is { } d) ids.Add(d);
            if (e.EngagementExecutiveId is { } x) ids.Add(x);
            if (e.BillingManagerId is { } b) ids.Add(b);
            foreach (var s in e.CommissionSplits.Where(s => !s.Deleted))
            {
                ids.Add(s.EmployeeId);
            }
        }
        return ids;
    }

    private async Task<HashSet<Guid>> ResolveOptionItemIdsAsync(string setKey, CancellationToken cancellationToken)
    {
        var tenantId = User.GetActiveTenantId();
        var set = await _optionSets.GetEffectiveSetAsync(tenantId, EntityType.Rems, setKey, cancellationToken);
        return set?.Items.Where(i => !i.Deleted).Select(i => i.Id).ToHashSet() ?? new HashSet<Guid>();
    }

    /// <summary>Upserts a shared <see cref="Address"/> from the input (creating one when <paramref name="existingId"/> is null); returns the id, or null when blank.</summary>
    private async Task<Guid?> UpsertAddressAsync(Guid? existingId, RemsAddressInput input, AddressType type, CancellationToken cancellationToken)
    {
        if (!input.HasAny)
        {
            return existingId; // nothing supplied — leave the current address untouched.
        }

        if (existingId is { } id && await _addresses.GetByIdAsync(id, cancellationToken) is { } address)
        {
            ApplyAddress(address, input);
            _addresses.Update(address);
            return address.Id;
        }

        var created = NewAddress(input, type);
        await _addresses.AddAsync(created, cancellationToken);
        return created.Id;
    }

    /// <summary>Upserts (or, when blank, removes) an entity address of a given type from a supplied input.</summary>
    private async Task UpsertEntityAddressAsync(REMSEntity entity, RemsAddressType type, RemsAddressInput? input, CancellationToken cancellationToken)
    {
        if (input is null)
        {
            return; // not supplied — leave unchanged.
        }

        var existing = entity.Addresses.FirstOrDefault(a => !a.Deleted && a.AddressType == type);
        if (!input.HasAny)
        {
            if (existing is not null)
            {
                _clients.RemoveEntityAddress(existing);
            }
            return;
        }

        if (existing?.Address is { } address)
        {
            ApplyAddress(address, input);
            _addresses.Update(address);
        }
        else
        {
            var created = NewAddress(input, AddressTypeFor(type));
            await _addresses.AddAsync(created, cancellationToken);
            await _clients.AddEntityAddressAsync(new REMSEntityAddress
            {
                Id = Guid.NewGuid(),
                REMSEntityId = entity.Id,
                AddressId = created.Id,
                AddressType = type,
            }, cancellationToken);
        }
    }

    /// <summary>Clones a source <see cref="Address"/> onto a target entity's address slot (upsert by type) — used by copy-from.</summary>
    private async Task UpsertEntityAddressFromAsync(REMSEntity entity, RemsAddressType type, Address source, CancellationToken cancellationToken)
    {
        var existing = entity.Addresses.FirstOrDefault(a => !a.Deleted && a.AddressType == type);
        if (existing?.Address is { } address)
        {
            address.AddressLine1 = source.AddressLine1;
            address.CityName = source.CityName;
            address.StateName = source.StateName;
            address.PostalCode = source.PostalCode;
            _addresses.Update(address);
        }
        else
        {
            var created = new Address
            {
                Id = Guid.NewGuid(),
                AddressType = AddressTypeFor(type),
                AddressLine1 = source.AddressLine1,
                CityName = source.CityName,
                StateName = source.StateName,
                PostalCode = source.PostalCode,
            };
            await _addresses.AddAsync(created, cancellationToken);
            await _clients.AddEntityAddressAsync(new REMSEntityAddress
            {
                Id = Guid.NewGuid(),
                REMSEntityId = entity.Id,
                AddressId = created.Id,
                AddressType = type,
            }, cancellationToken);
        }
    }

    private static Address NewAddress(RemsAddressInput input, AddressType type)
    {
        var address = new Address { Id = Guid.NewGuid(), AddressType = type };
        ApplyAddress(address, input);
        return address;
    }

    /// <summary>Copies the standard address block onto a shared <see cref="Address"/> (create and update alike).</summary>
    private static void ApplyAddress(Address address, RemsAddressInput input)
    {
        address.AddressLine1 = Normalize(input.Street);
        address.AddressLine2 = Normalize(input.AddressLine2);
        address.CityName = Normalize(input.City);
        address.StateCode = Normalize(input.StateCode);
        address.StateName = Normalize(input.State);
        address.CountryCode = Normalize(input.CountryCode);
        address.CountryName = Normalize(input.CountryName);
        address.PostalCode = Normalize(input.Zip);
    }

    private static AddressType AddressTypeFor(RemsAddressType type)
        => type == RemsAddressType.Physical ? AddressType.Office : AddressType.Other;

    private static Person NewContactPerson(RemsEntityContactInput input)
    {
        var person = new Person
        {
            Id = Guid.NewGuid(),
            PersonCode = "PER-" + Guid.NewGuid().ToString("N").ToUpperInvariant(),
            IsActive = true,
        };
        ApplyContactPerson(person, input);
        return person;
    }

    private static void ApplyContactPerson(Person person, RemsEntityContactInput input)
    {
        var (first, last) = SplitName(input.Name);
        person.FirstName = first;
        person.LastName = last;
        person.DisplayName = Normalize(input.Name) ?? first;
        person.PrimaryEmail = Normalize(input.Email);
        person.MobileNumber = Normalize(input.Phone);
        person.LastProfileUpdatedOn = DateTime.UtcNow;
    }

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

    private IActionResult EngagementLocked()
        => StatusCode(StatusCodes.Status409Conflict, ApiResponseFactory.Error(
            CodeEngagementLocked, "This engagement is locked.", "The engagement cannot be edited while it is pending approval or approved."));

    private IActionResult CopyInvalid(string message)
        => StatusCode(StatusCodes.Status409Conflict, ApiResponseFactory.Error(CodeCopyInvalid, message, message));

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
