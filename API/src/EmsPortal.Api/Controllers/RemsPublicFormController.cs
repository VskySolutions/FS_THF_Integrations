using System.Globalization;
using EmsPortal.Api.Models.Rems;
using EmsPortal.Api.Validators.Rems;
using EmsPortal.Application.Abstractions.Email;
using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Application.Abstractions.Tenancy;
using EmsPortal.Application.Abstractions.UniversalFeatures;
using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;
using EmsPortal.Shared.Configuration;
using EmsPortal.Shared.Contracts;
using EmsPortal.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace EmsPortal.Api.Controllers;

/// <summary>
/// The public, unauthenticated REMS client onboarding form (WO-113). A client follows the emailed invite
/// link (<c>{inviteCode}</c>) to load, auto-save, review and finally submit their EMS form; the submission
/// transactionally materialises the client, entities, addresses, contacts and blank engagements.
/// <para>
/// These endpoints are <see cref="AllowAnonymousAttribute">anonymous</see>: there is NO resolved tenant and
/// NO current user. The form is resolved by invite code ALONE (a 128-bit unguessable value); the form's own
/// <see cref="REMSForm.TenantId"/> is then the authoritative tenant for everything read or written. Because
/// <c>EmsPortalDbContext.StampTenant()</c> no-ops without a resolved tenant, the tenant is established from
/// the form (<see cref="ITenantContext.Set"/>) and, defensively, <c>TenantId</c> is ALSO set explicitly on
/// every REMS / Person row created — never <see cref="Guid.Empty"/>. Responses disclose only this form's own
/// prefill / draft; a bad or inactive link returns a generic state with no other-tenant information.
/// </para>
/// </summary>
[ApiController]
[Route("api/rems/public/forms")]
[AllowAnonymous]
[Produces("application/json")]
[Tags("REMS Public Form")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status500InternalServerError)]
public sealed class RemsPublicFormController : ControllerBase
{
    private const string CodeNotEditable = "REMS_FORM_NOT_EDITABLE";

    private static readonly RemsFormPayloadValidator PayloadValidator = new();

    private readonly IRemsFormRepository _forms;
    private readonly IRemsRepository _rems;
    private readonly IRemsClientRepository _clients;
    private readonly IRemsEngagementRepository _engagements;
    private readonly IAddressRepository _addresses;
    private readonly IPersonRepository _persons;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantContext _tenantContext;
    private readonly IActivityEventWriter _activity;
    private readonly INotificationDispatcher _notifications;
    private readonly IRemsEmailNotifier _emailNotifier;
    private readonly string _baseUrl;
    private readonly IOptionSetRepository _optionSets;
    private readonly ILogger<RemsPublicFormController> _logger;

    public RemsPublicFormController(
        IRemsFormRepository forms,
        IRemsRepository rems,
        IRemsClientRepository clients,
        IRemsEngagementRepository engagements,
        IAddressRepository addresses,
        IPersonRepository persons,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        ITenantContext tenantContext,
        IActivityEventWriter activity,
        INotificationDispatcher notifications,
        IRemsEmailNotifier emailNotifier,
        IOptions<AppOptions> appOptions,
        IOptionSetRepository optionSets,
        ILogger<RemsPublicFormController> logger)
    {
        _forms = forms;
        _rems = rems;
        _clients = clients;
        _engagements = engagements;
        _addresses = addresses;
        _persons = persons;
        _users = users;
        _unitOfWork = unitOfWork;
        _tenantContext = tenantContext;
        _activity = activity;
        _notifications = notifications;
        _emailNotifier = emailNotifier;
        _baseUrl = appOptions.Value.BaseUrl;
        _optionSets = optionSets;
        _logger = logger;
    }

    // -------------------- Load --------------------

    /// <summary>
    /// Resolve the public form and return its load state (WO-113): <c>Invalid</c> (bad link),
    /// <c>Unavailable</c> (request deleted, cancelled, or not yet sent), <c>Submitted</c> (thank-you), or
    /// <c>Editable</c> (industry group + locked prefill + any saved draft). Always HTTP 200; the state — not
    /// the status code — drives the client, and nothing about other requests is disclosed.
    /// </summary>
    [HttpGet("{inviteCode}")]
    [ProducesResponseType<ApiResponse<RemsPublicFormResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Load(string inviteCode, CancellationToken cancellationToken)
    {
        var form = await LoadFormAsync(inviteCode, cancellationToken);
        if (form is null)
        {
            return Ok(ApiResponseFactory.Success(new RemsPublicFormResponse(RemsPublicFormStates.Invalid), "REMS form resolved."));
        }

        var rems = form.Rems!;
        if (IsUnavailable(form))
        {
            return Ok(ApiResponseFactory.Success(new RemsPublicFormResponse(RemsPublicFormStates.Unavailable), "REMS form resolved."));
        }

        return form.Status switch
        {
            RemsFormStatus.Submitted => Ok(ApiResponseFactory.Success(
                new RemsPublicFormResponse(RemsPublicFormStates.Submitted, ClientName: rems.ClientDisplayName),
                "REMS form resolved.")),

            RemsFormStatus.Sent => Ok(ApiResponseFactory.Success(
                new RemsPublicFormResponse(
                    RemsPublicFormStates.Editable,
                    IndustryGroup: form.IndustryGroup,
                    Prefill: BuildPrefill(rems),
                    DraftPayload: RemsFormPayloadJson.TryDeserialize(CurrentDraft(form)?.DraftPayload),
                    ReferralSources: await ResolvePublicOptionsAsync(rems.TenantId, "REMS.ReferralSource", cancellationToken)),
                "REMS form resolved.")),

            // Draft / Saved: built but not yet sent — the link is not active until the Admin sends it.
            _ => Ok(ApiResponseFactory.Success(new RemsPublicFormResponse(RemsPublicFormStates.Unavailable), "REMS form resolved.")),
        };
    }

    /// <summary>
    /// Resolves an option list for the anonymous form, scoped to the REQUEST's tenant rather than to an
    /// ambient one — this caller holds an invite code, not a session, so there is no tenant context to
    /// read. Falls back to the tenant's effective list (their own copy, else the platform standard),
    /// exactly as the staff resolve endpoint does, so the client sees the same wording staff maintain.
    /// An absent list yields null and the form falls back to its built-in copy rather than showing an
    /// empty picker.
    /// </summary>
    private async Task<IReadOnlyList<RemsPublicOption>?> ResolvePublicOptionsAsync(
        Guid tenantId, string key, CancellationToken cancellationToken)
    {
        var set = await _optionSets.GetEffectiveSetAsync(tenantId, EntityType.Rems, key, cancellationToken);
        if (set is null)
        {
            return null;
        }

        var ordered = set.ItemSortMode switch
        {
            OptionItemSortMode.AlphabeticalAsc => set.Items.OrderBy(i => i.Label),
            OptionItemSortMode.AlphabeticalDesc => set.Items.OrderByDescending(i => i.Label),
            _ => set.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Label),
        };

        var options = ordered
            .Where(i => i.IsActive && !i.Deleted)
            .Select(i => new RemsPublicOption(i.Value, i.Label, i.Description))
            .ToList();
        return options.Count > 0 ? options : null;
    }

    // -------------------- Draft (auto-save / explicit save) --------------------

    /// <summary>
    /// Upsert the single in-progress draft (WO-113). Accepts a partial <see cref="RemsFormPayloadV1"/> — no
    /// industry-group validation runs here — and stores it durably so it survives across visits. Allowed only
    /// while the form is editable (Sent); rejected once submitted or otherwise unavailable.
    /// </summary>
    [HttpPut("{inviteCode}/draft")]
    [ProducesResponseType<ApiResponse<RemsDraftSavedResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveDraft(string inviteCode, [FromBody] RemsFormPayloadV1 payload, CancellationToken cancellationToken)
    {
        var form = await LoadFormAsync(inviteCode, cancellationToken);
        if (form is null)
        {
            return FormNotFound();
        }
        if (!IsEditable(form))
        {
            return NotEditable();
        }

        var now = DateTime.UtcNow;
        var json = RemsFormPayloadJson.Serialize(payload);
        var draft = CurrentDraft(form);
        if (draft is null)
        {
            draft = new REMSFormDraft
            {
                Id = Guid.NewGuid(),
                TenantId = form.TenantId, // EXPLICIT — no tenant stamping in the public context.
                REMSFormId = form.Id,
                DraftPayload = json,
                LastSavedOnUtc = now,
            };
            await _forms.AddDraftAsync(draft, cancellationToken);
        }
        else
        {
            draft.DraftPayload = json;
            draft.LastSavedOnUtc = now;
            _forms.UpdateDraft(draft);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(new RemsDraftSavedResponse(now), "Draft saved."));
    }

    // -------------------- Review --------------------

    /// <summary>
    /// Validate the supplied (or stored) payload against the full industry-group rules and return the
    /// read-only review presentation, grouped as Contact · Contract Details (Government only) · Other
    /// Entities · Address · Additional Contacts · Billing (AC-REMS-024.7). On any failure the validation
    /// errors are returned instead, so the client cannot reach review with an invalid form (AC-REMS-024.8).
    /// </summary>
    [HttpPost("{inviteCode}/review")]
    [ProducesResponseType<ApiResponse<RemsReviewModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Review(
        string inviteCode,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RemsFormPayloadV1? payload,
        CancellationToken cancellationToken)
    {
        var form = await LoadFormAsync(inviteCode, cancellationToken);
        if (form is null)
        {
            return FormNotFound();
        }
        if (!IsEditable(form))
        {
            return NotEditable();
        }

        var effective = ResolvePayload(payload, form);
        var validation = PayloadValidator.Validate(effective, form.IndustryGroup);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponseFactory.ValidationError(validation.Errors));
        }

        var model = BuildReviewModel(form.Rems!, effective!, form.IndustryGroup);
        return Ok(ApiResponseFactory.Success(model, "REMS form review ready."));
    }

    // -------------------- Submit --------------------

    /// <summary>
    /// Transactionally submit the form (WO-113). Re-validates server-side, then in one transaction snapshots
    /// the immutable submission, locks the form (Status=Submitted), flips the request to
    /// <c>customer_submitted</c>, and materialises the client, its entities, addresses, contacts and blank
    /// engagements (plus the government contract detail). Idempotent: an already-submitted form returns the
    /// thank-you state without creating anything. After commit, best-effort in-app + email notifications go to
    /// the assigned Admin and CSE.
    /// </summary>
    [HttpPost("{inviteCode}/submit")]
    [ProducesResponseType<ApiResponse<RemsPublicFormResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Submit(
        string inviteCode,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RemsFormPayloadV1? payload,
        CancellationToken cancellationToken)
    {
        var form = await LoadFormAsync(inviteCode, cancellationToken);
        if (form is null)
        {
            return FormNotFound();
        }

        var rems = form.Rems!;

        // Idempotency: an already-submitted form returns the thank-you state — never a duplicate, never a reset.
        if (form.Status == RemsFormStatus.Submitted)
        {
            return Ok(ApiResponseFactory.Success(
                new RemsPublicFormResponse(RemsPublicFormStates.Submitted, ClientName: rems.ClientDisplayName),
                "REMS form already submitted."));
        }
        if (!IsEditable(form))
        {
            return NotEditable();
        }

        var effective = ResolvePayload(payload, form);

        // Re-validate everything server-side (AC-REMS-024.8) before any write.
        var validation = PayloadValidator.Validate(effective, form.IndustryGroup);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponseFactory.ValidationError(validation.Errors));
        }

        await SubmitTransactionAsync(form, effective!, cancellationToken);
        await DispatchPostSubmitAsync(form, effective!, cancellationToken);

        return Ok(ApiResponseFactory.Success(
            new RemsPublicFormResponse(
                RemsPublicFormStates.Submitted,
                ClientName: NullIfBlank(effective!.EffectiveClientName) ?? rems.ClientDisplayName),
            "REMS form submitted."));
    }

    // -------------------- Cancel --------------------

    /// <summary>
    /// Acknowledge a client's cancellation of the form (AC-REMS-010.9) — a client-side confirmation only.
    /// Non-destructive: the draft is kept and the form is NOT locked, so the client can return via the same
    /// link and continue. Returns a simple acknowledgement.
    /// </summary>
    [HttpPost("{inviteCode}/cancel")]
    [ProducesResponseType<ApiResponse<RemsPublicCancelResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(string inviteCode, CancellationToken cancellationToken)
    {
        var form = await LoadFormAsync(inviteCode, cancellationToken);
        if (form is null)
        {
            return FormNotFound();
        }

        // Intentionally non-destructive: no state change, no draft deletion — the client may resume later.
        return Ok(ApiResponseFactory.Success(new RemsPublicCancelResponse(true), "Cancellation acknowledged."));
    }

    // -------------------- Submit transaction --------------------

    private async Task SubmitTransactionAsync(REMSForm form, RemsFormPayloadV1 payload, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // Build the ENTIRE object graph up front (stable, pre-generated ids). Persisting a fully-built graph
        // means a re-executed transaction (connection resiliency) re-adds the SAME instances rather than
        // duplicate-keyed new ones, and keeps the transaction body a flat, ordered sequence of inserts.
        // The request's one engagement, created when the initiator first saved it. Only the Government
        // contract dates need it — they come from the client's answers but belong to the engagement.
        var engagement = await _engagements.GetByRemsIdAsync(form.REMSId, cancellationToken);
        var graph = BuildSubmitGraph(form, payload, now, engagement?.Id);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // 2. Immutable submission snapshot (the (TenantId, REMSFormId) unique index is the duplicate backstop).
            await _forms.AddSubmissionAsync(graph.Submission, ct);

            // 4-7. The materialised client aggregate. Addresses/Persons are inserted before the rows that
            // reference them so the FKs resolve within the one transaction.
            foreach (var address in graph.Addresses)
            {
                await _addresses.AddAsync(address, ct);
            }
            foreach (var person in graph.Persons)
            {
                await _persons.AddAsync(person, ct);
            }

            await _clients.AddAsync(graph.Client, ct);
            foreach (var entity in graph.Entities)
            {
                await _clients.AddEntityAsync(entity, ct);
            }
            foreach (var entityAddress in graph.EntityAddresses)
            {
                await _clients.AddEntityAddressAsync(entityAddress, ct);
            }
            foreach (var contact in graph.Contacts)
            {
                await _clients.AddEntityContactAsync(contact, ct);
            }
            foreach (var additional in graph.AdditionalEntities)
            {
                await _rems.AddAdditionalEntityAsync(additional, ct);
            }
            if (graph.GovernmentDetail is not null)
            {
                await _engagements.AddGovernmentDetailAsync(graph.GovernmentDetail, ct);
            }

            // 3. Lock the form + flip the request status. The form and its request were loaded tracked, so
            // mutating their scalars is picked up by change tracking — no explicit Update (which would also
            // needlessly re-write the loaded draft rows through graph traversal).
            form.Status = RemsFormStatus.Submitted;
            form.SubmittedOnUtc = now;
            form.InviteLockedOnUtc ??= now;
            // The client's answers are in, so the request passes to the Admin the initiator named. The
            // engagement setup was filled before any of this, so what happens next is review, not setup.
            form.Rems!.Status = RemsRequestStatuses.AdminReview;

            // 8. Commit.
            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    /// <summary>
    /// Materialises the whole submit graph (submission, client, entities, addresses, contacts, engagements,
    /// government detail) with explicit TenantId on every REMS/Person row and pre-generated ids. Pure (no
    /// I/O) so it can run outside the transaction.
    /// </summary>
    private SubmitGraph BuildSubmitGraph(REMSForm form, RemsFormPayloadV1 payload, DateTime now, Guid? engagementId)
    {
        var tenantId = form.TenantId;
        var isBusiness = RemsFormPayloadValidator.IsBusinessGroup(form.IndustryGroup);
        var isGovernment = string.Equals(form.IndustryGroup, RemsFormPayloadValidator.Government, StringComparison.Ordinal);

        var submissionId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var graph = new SubmitGraph
        {
            Submission = new REMSFormSubmission
            {
                Id = submissionId,
                TenantId = tenantId,
                REMSFormId = form.Id,
                SubmittedPayload = RemsFormPayloadJson.Serialize(payload),
                SubmittedOnUtc = now,
            },
        };

        // The billing ADDRESS is no longer staged here — it is one of the main entity's three addresses now
        // (see StageEntityAddresses). Only the billing contact's name and email stay on the client.
        // Email is LOCKED to the request — the payload email is never read on submit.
        graph.Client = new REMSClient
        {
            Id = clientId,
            TenantId = tenantId,
            REMSId = form.REMSId,
            SourceFormSubmissionId = submissionId,
            // Points at the same Person the request's client resolved to at intake, so the client the
            // form materialises and the client the picker knows are one record rather than two.
            ExternalClientReferenceId = form.Rems!.ClientPersonId,
            // EffectiveClientName, not ClientName: an individual gives their name in two boxes now, and
            // the joined echo beside them is the client's to send rather than ours to trust.
            Name = Clean(payload.EffectiveClientName) ?? string.Empty,
            Email = form.Rems!.CustomerEmail ?? string.Empty, // LOCKED to the request's customer email.
            MobileNumber = Clean(payload.MobileNumber),
            ReferralSource = Clean(payload.ReferralSource),
            ReferralSourceDetail = Clean(payload.ReferralSourceDetail),
            BillingContactName = Clean(payload.BillingContactName),
            BillingEmail = Clean(payload.BillingEmail),
        };

        // Main entity + its addresses, role contacts and (Government) contract detail. No engagement is
        // staged: the request already has one, created when the initiator first saved it, and the
        // government contract dates simply attach to it.
        var mainEntityId = Guid.NewGuid();
        graph.Entities.Add(new REMSEntity
        {
            Id = mainEntityId,
            TenantId = tenantId,
            REMSClientId = clientId,
            SourceEntityKey = "main",
            Name = Clean(payload.EffectiveClientName) ?? string.Empty,
            EIN = isBusiness ? Clean(payload.Ein) : null,
            IsMainEntity = true,
        });
        StageEntityAddresses(
            graph, tenantId, mainEntityId,
            payload.PhysicalAddress, payload.MailingAddress, payload.BillingAddress);
        StageRoleContacts(graph, tenantId, form.REMSId, form.IndustryGroup, mainEntityId, payload.Roles);

        // Guarded on the engagement existing: a request written before the setup moved to the front could
        // reach here without one, and a contract detail with nothing to hang off would fail the insert.
        if (isGovernment && engagementId is { } governmentEngagementId)
        {
            graph.GovernmentDetail = new REMSEngagementGovernmentDetail
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                REMSEngagementId = governmentEngagementId,
                ContractStartDate = payload.ContractStartDate,
                ContractEndDate = payload.ContractEndDate,
                OriginalTerm = Clean(payload.OriginalTerm),
                RenewalTerms = Clean(payload.RenewalTerms),
                PurchaseOrderStartDate = payload.PoStartDate,
                PurchaseOrderEndDate = payload.PoEndDate,
            };
        }

        // The client's other businesses, as contacts on the REQUEST rather than entities under the client.
        // Each one is a prompt for its own REMS request, raised by hand from the Partner/CSE list — so
        // there is no entity, no address and no engagement to stage here, and nothing that fans this
        // request out into several approvals.
        var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "main" };
        var fallbackIndex = 0;
        foreach (var related in payload.RelatedEntities)
        {
            graph.AdditionalEntities.Add(new REMSAdditionalEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                REMSId = form.REMSId,
                SourceKey = UniqueEntityKey(related.SourceKey, usedKeys, ref fallbackIndex),
                FullName = Clean(related.FullName) ?? string.Empty,
                EmailAddress = Clean(related.EmailAddress),
                PhoneNumber = Clean(related.PhoneNumber),
            });
        }

        return graph;
    }

    /// <summary>
    /// Stages the entity's physical, mailing and billing addresses. All three are written whenever the
    /// client supplied them — the form offers "copy from" rather than a differs/hide toggle, and a copied
    /// address is a snapshot the client can then edit, so each one is stored in its own right. Under the
    /// old toggle an unticked "mailing differs" wrote no mailing row at all, which meant correcting the
    /// physical address silently moved the mailing address with it.
    /// </summary>
    private static void StageEntityAddresses(
        SubmitGraph graph, Guid tenantId, Guid entityId,
        RemsAddressPayload? physical, RemsAddressPayload? mailing, RemsAddressPayload? billing)
    {
        Stage(physical, AddressType.Office, RemsAddressType.Physical);
        Stage(mailing, AddressType.Other, RemsAddressType.Mailing);
        Stage(billing, AddressType.Billing, RemsAddressType.Billing);

        void Stage(RemsAddressPayload? payload, AddressType addressType, RemsAddressType remsType)
        {
            if (payload is not { HasAny: true })
            {
                return;
            }
            var address = NewAddress(payload, addressType);
            graph.Addresses.Add(address);
            graph.EntityAddresses.Add(NewEntityAddress(tenantId, entityId, address.Id, remsType));
        }
    }

    private static void StageRoleContacts(
        SubmitGraph graph, Guid tenantId, Guid sourceRemsId, string industryGroup, Guid entityId, RemsRolesPayload? roles)
    {
        if (roles is null)
        {
            return;
        }

        foreach (var (role, roleName, isRequired) in EnumerateRoles(industryGroup, roles.Normalized()))
        {
            if (role is not { HasAny: true })
            {
                continue;
            }

            var person = BuildContactPerson(tenantId, sourceRemsId, role);
            graph.Persons.Add(person);
            graph.Contacts.Add(new REMSEntityContact
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                REMSEntityId = entityId,
                PersonId = person.Id,
                ContactRole = roleName,
                IsRequired = isRequired,
            });
        }
    }

    /// <summary>
    /// Satisfies the required <c>REMSEntityContact.PersonId</c> FK by creating a minimal tenant-scoped
    /// <see cref="Person"/> from the role's name / email / phone. The public form collects a contact, not a
    /// platform account, so this is the lightest record that satisfies the FK: TenantId is set EXPLICITLY
    /// (no stamping without a resolved tenant) and PersonCode is a fresh GUID-derived code (globally unique
    /// by the filtered unique index, so no existence pre-check is needed).
    /// <para>
    /// Provenance is stamped with the originating request. These are the least deliberate persons the
    /// platform creates — a client typed them into a public form — so being able to tell them apart from
    /// staff-entered records, and trace them back to the form they came off, matters most here.
    /// </para>
    /// </summary>
    private static Person BuildContactPerson(Guid tenantId, Guid sourceRemsId, RemsRolePayload role)
    {
        // The form asks for the two parts, so the Person is filed under what the client actually typed
        // into them. Splitting on the first space is now only the fallback, for a payload written before
        // the name was two boxes — see RemsRolePayload.EffectiveFirstName.
        var first = role.EffectiveFirstName;
        var last = role.EffectiveLastName;
        return new Person
        {
            Id = Guid.NewGuid(),
            PersonCode = "PER-" + Guid.NewGuid().ToString("N").ToUpperInvariant(),
            TenantId = tenantId,
            SourceEntityType = EntityType.Rems,
            SourceEntityId = sourceRemsId,
            FirstName = first,
            LastName = last,
            DisplayName = Clean(role.DisplayName) ?? first,
            PrimaryEmail = Clean(role.Email),
            MobileNumber = Clean(role.Phone),
            IsActive = true,
            LastProfileUpdatedOn = DateTime.UtcNow,
        };
    }

    // StageBlankEngagement is gone. Engagements are no longer minted on submit: the initiator fills the
    // engagement setup before the client is ever contacted, so by the time a submission arrives the
    // request already has its one engagement and this path only attaches the client's answers to it.

    /// <summary>The staged submit graph — every row carries an explicit TenantId and a pre-generated id.</summary>
    private sealed class SubmitGraph
    {
        public required REMSFormSubmission Submission { get; init; }
        public REMSClient Client { get; set; } = null!;
        public List<Address> Addresses { get; } = new();
        public List<Person> Persons { get; } = new();
        public List<REMSEntity> Entities { get; } = new();
        public List<REMSEntityAddress> EntityAddresses { get; } = new();
        public List<REMSEntityContact> Contacts { get; } = new();
        public List<REMSAdditionalEntity> AdditionalEntities { get; } = new();
        public REMSEngagementGovernmentDetail? GovernmentDetail { get; set; }
    }

    // -------------------- Post-commit (best-effort) --------------------

    /// <summary>
    /// After the submission is durably committed, notify the assigned Admin and CSE in-app and by email
    /// (AC-REMS ...). Best-effort: any failure here is logged and swallowed and never rolls the submission
    /// back. The tenant context was established from the form, so the staged notification/activity rows
    /// stamp with the correct tenant.
    /// </summary>
    private async Task DispatchPostSubmitAsync(REMSForm form, RemsFormPayloadV1 payload, CancellationToken cancellationToken)
    {
        var rems = form.Rems!;
        // Assigned admin, CSE, and the requester — the customer coming back is the milestone the person who
        // raised the request is waiting on. The admin half is empty on a request nobody has picked up yet,
        // which is the ordinary case: the broadcast below is what tells the admins about those.
        var recipients = new[] { rems.AdminAssignedToId, rems.CSEId, rems.CreatedById }
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        try
        {
            foreach (var userId in recipients)
            {
                await _notifications.DispatchAsync(new CreateNotificationDto(
                    userId,
                    NotificationType.RemsFormSubmitted,
                    "A REMS onboarding form was submitted",
                    $"{rems.REMSNumber} — {rems.ClientDisplayName}",
                    EntityType.Rems,
                    rems.Id), cancellationToken);
            }

            // Nobody has claimed this request, so the answers that just landed are on no admin's desk in
            // particular. Every admin in the tenant is told it is waiting, which is the in-app half of what
            // EMS Review shows as "Waiting for pickup" — without it a submission on an unclaimed request
            // would reach the initiator and the CSE and no admin at all.
            if (rems.AdminAssignedToId is null)
            {
                var admins = await _users.ListByTenantRolesAsync(
                    form.TenantId, new[] { Roles.Admin, Roles.SuperAdmin }, cancellationToken);
                foreach (var admin in admins.Where(a => !recipients.Contains(a.Id)))
                {
                    await _notifications.DispatchAsync(new CreateNotificationDto(
                        admin.Id,
                        NotificationType.RemsRequestAssigned,
                        "A REMS request is waiting for pickup",
                        $"{rems.REMSNumber} — {rems.ClientDisplayName}",
                        EntityType.Rems,
                        rems.Id), cancellationToken);
                }
            }

            await _activity.WriteAsync(
                new CreateActivityEventDto(EntityType.Rems, rems.Id, ActivityEventTypes.RemsFormSubmitted), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // External "form submitted" email to the Admin + CSE. Enqueued on a worker; never throws/blocks.
            var model = new RemsFormSubmittedEmail(
                Clean(payload.EffectiveClientName) ?? rems.ClientDisplayName,
                rems.REMSNumber,
                $"{_baseUrl.TrimEnd('/')}/rems/requests/{rems.Id}",
                (form.SubmittedOnUtc ?? DateTime.UtcNow).ToString("f", CultureInfo.InvariantCulture) + " UTC");

            foreach (var userId in recipients)
            {
                var user = await _users.GetByIdAsync(userId, cancellationToken);
                if (!string.IsNullOrWhiteSpace(user?.Email))
                {
                    _emailNotifier.SendFormSubmitted(form.TenantId, user!.Email, model);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex, "REMS form {FormId} was submitted, but post-commit notification/email dispatch failed.", form.Id);
        }
    }

    // -------------------- Review model --------------------

    private RemsReviewModel BuildReviewModel(REMS rems, RemsFormPayloadV1 payload, string industryGroup)
    {
        var contact = new RemsReviewContact(
            Clean(payload.EffectiveClientName), Clean(payload.ClientFirstName), Clean(payload.ClientLastName),
            rems.CustomerEmail ?? string.Empty, Clean(payload.MobileNumber), Clean(payload.ReferralSource));

        var contract = string.Equals(industryGroup, RemsFormPayloadValidator.Government, StringComparison.Ordinal)
            ? new RemsReviewContractDetails(
                payload.ContractStartDate, payload.ContractEndDate, Clean(payload.OriginalTerm),
                Clean(payload.RenewalTerms), payload.PoStartDate, payload.PoEndDate)
            : null;

        var others = payload.RelatedEntities
            .Select(r => new RemsReviewOtherEntity(
                Clean(r.SourceKey), Clean(r.FullName), Clean(r.EmailAddress), Clean(r.PhoneNumber)))
            .ToList();

        // All three addresses are shown as given. The old "mailing differs" flag decided whether there was
        // a mailing address at all; the client now copies one forward and edits it, so each stands alone.
        var address = new RemsReviewAddressGroup(
            NonEmpty(payload.PhysicalAddress), NonEmpty(payload.MailingAddress), NonEmpty(payload.BillingAddress));

        var additionalContacts = EnumerateRoles(industryGroup, payload.EffectiveRoles)
            .Where(t => t.Role is { HasAny: true })
            .Select(t => new RemsReviewContactRow(
                t.RoleName, t.IsRequired,
                Clean(t.Role!.EffectiveFirstName), Clean(t.Role.EffectiveLastName), Clean(t.Role.DisplayName),
                Clean(t.Role.Email), Clean(t.Role.Phone)))
            .ToList();

        var billing = new RemsReviewBilling(Clean(payload.BillingContactName), Clean(payload.BillingEmail), NonEmpty(payload.BillingAddress));

        return new RemsReviewModel(contact, contract, others, address, additionalContacts, billing);
    }

    // -------------------- Helpers --------------------

    /// <summary>Loads the form by invite code (unscoped) and, once resolved, pins the tenant context to its tenant.</summary>
    private async Task<REMSForm?> LoadFormAsync(string inviteCode, CancellationToken cancellationToken)
    {
        var form = await _forms.GetByInviteCodeUnscopedAsync(inviteCode?.Trim() ?? string.Empty, cancellationToken);
        if (form is not null && form.TenantId != Guid.Empty)
        {
            // Establish the resolved tenant so DbContext stamping and the notification/activity writers behave
            // exactly as they do for an authenticated request. Every write also sets TenantId explicitly.
            _tenantContext.Set(form.TenantId, string.Empty);
        }

        return form;
    }

    /// <summary>
    /// The locked prefill for the editable form. The client's name arrives both whole and split: staff
    /// intake asks for it in one box, an individual's form asks for it in two, and doing the split here
    /// keeps it the same split their Person record and their contacts already get.
    /// </summary>
    private static RemsPublicPrefill BuildPrefill(REMS rems)
    {
        // Off the DISPLAY name, so a client whose request names them "John Smith Jr." is not prefilled as
        // plain "John Smith" — and the suffix lands on the last name, where a person's name carries it.
        var (first, last) = RemsNameSplit.Split(rems.ClientDisplayName);
        return new RemsPublicPrefill(
            rems.ClientDisplayName, first, last, rems.CustomerEmail ?? string.Empty, rems.CustomerMobileNumber);
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>The link is dead when the request is gone/deleted or the form was cancelled.</summary>
    private static bool IsUnavailable(REMSForm form)
        => form.Rems is null || form.Rems.Deleted || form.Status == RemsFormStatus.Cancelled;

    /// <summary>Editable == sent-and-live: the only state that accepts draft saves, review and submit.</summary>
    private static bool IsEditable(REMSForm form)
        => !IsUnavailable(form) && form.Status == RemsFormStatus.Sent;

    /// <summary>The single active draft for the form (query filters were ignored on load, so exclude soft-deleted).</summary>
    private static REMSFormDraft? CurrentDraft(REMSForm form)
        => form.Drafts.FirstOrDefault(d => !d.Deleted);

    /// <summary>The payload to act on: the request body when supplied, else the stored draft.</summary>
    private static RemsFormPayloadV1? ResolvePayload(RemsFormPayloadV1? body, REMSForm form)
        => body ?? RemsFormPayloadJson.TryDeserialize(CurrentDraft(form)?.DraftPayload);

    /// <summary>The relevant (role, canonical role name, required?) tuples for an industry group.</summary>
    private static IEnumerable<(RemsRolePayload? Role, string RoleName, bool IsRequired)> EnumerateRoles(
        string industryGroup, RemsRolesPayload roles)
    {
        // Mirrors RemsFormPayloadValidator's branches exactly — the roles staged here must be the roles it
        // required. if/else rather than a switch because the business branch matches a family of codes.
        // Takes the roles already NORMALIZED (see RemsRolesPayload.Normalized) — a form filled in under
        // the old business role names still stages its three contacts, under the names they are known by
        // now.
        if (industryGroup == RemsFormPayloadValidator.Individual)
        {
            yield return (roles.Self, nameof(RemsContactRole.Self), true);
            yield return (roles.Spouse, nameof(RemsContactRole.Spouse), false);
        }
        else if (RemsFormPayloadValidator.IsBusinessGroup(industryGroup))
        {
            yield return (roles.PrimaryContact, nameof(RemsContactRole.PrimaryClientContact), true);
            yield return (roles.FinancialContact, nameof(RemsContactRole.FinancialContact), true);
            yield return (roles.BillingContact, nameof(RemsContactRole.BillingContact), true);
            yield return (roles.OtherContact, nameof(RemsContactRole.OtherContact), false);
            // Retired from the form. Still staged when an older payload carries one, because the contact
            // the client gave is a contact whether or not the box is still on the page.
            yield return (roles.Banker, nameof(RemsContactRole.Banker), false);
            yield return (roles.Lawyer, nameof(RemsContactRole.Lawyer), false);
        }
        else if (industryGroup == RemsFormPayloadValidator.Government)
        {
            yield return (roles.FinanceDirector, nameof(RemsContactRole.FinanceDirector), true);
            yield return (roles.BillingContact, nameof(RemsContactRole.BillingContact), false);
            yield return (roles.OtherContact, nameof(RemsContactRole.OtherContact), false);
        }
    }

    /// <summary>A per-client-unique, non-"main" source key for a related entity; never trusts the client value blindly.</summary>
    private static string UniqueEntityKey(string? supplied, HashSet<string> used, ref int fallbackIndex)
    {
        var candidate = supplied?.Trim();
        if (string.IsNullOrEmpty(candidate) || string.Equals(candidate, "main", StringComparison.OrdinalIgnoreCase) || used.Contains(candidate))
        {
            do
            {
                candidate = $"related-{++fallbackIndex}";
            }
            while (used.Contains(candidate));
        }

        // Cap to the column length (64) and record it as taken.
        candidate = candidate.Length > 64 ? candidate[..64] : candidate;
        used.Add(candidate);
        return candidate;
    }

    private static Address NewAddress(RemsAddressPayload payload, AddressType type) => new()
    {
        Id = Guid.NewGuid(),
        AddressType = type,
        AddressLine1 = Clean(payload.Street),
        AddressLine2 = Clean(payload.AddressLine2),
        CityName = Clean(payload.City),
        StateCode = Clean(payload.StateCode),
        StateName = Clean(payload.State),
        CountryCode = Clean(payload.CountryCode),
        CountryName = Clean(payload.CountryName),
        PostalCode = Clean(payload.Zip),
    };

    private static REMSEntityAddress NewEntityAddress(Guid tenantId, Guid entityId, Guid addressId, RemsAddressType type) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        REMSEntityId = entityId,
        AddressId = addressId,
        AddressType = type,
    };

    // SplitName stood here. The public form asks for a first and a last name in their own boxes now, so
    // there is nothing left here to split; the one-box fallback for older payloads lives on the payload
    // itself (RemsNameSplit, behind RemsRolePayload.EffectiveFirstName).

    private static RemsAddressPayload? NonEmpty(RemsAddressPayload? address) => address is { HasAny: true } ? address : null;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private IActionResult FormNotFound()
        => NotFound(ApiResponseFactory.NotFound("This form is not available."));

    private IActionResult NotEditable()
        => StatusCode(StatusCodes.Status409Conflict, ApiResponseFactory.Error(
            CodeNotEditable, "This form is not available.", "The form is not currently accepting changes."));
}
