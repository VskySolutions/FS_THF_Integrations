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
    private readonly ILogger<RemsPublicFormController> _logger;

    public RemsPublicFormController(
        IRemsFormRepository forms,
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
        ILogger<RemsPublicFormController> logger)
    {
        _forms = forms;
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
                new RemsPublicFormResponse(RemsPublicFormStates.Submitted, ClientName: rems.RequestedClientName),
                "REMS form resolved.")),

            RemsFormStatus.Sent => Ok(ApiResponseFactory.Success(
                new RemsPublicFormResponse(
                    RemsPublicFormStates.Editable,
                    IndustryGroup: form.IndustryGroup,
                    Prefill: new RemsPublicPrefill(rems.RequestedClientName, rems.CustomerEmail ?? string.Empty, rems.CustomerMobileNumber),
                    DraftPayload: RemsFormPayloadJson.TryDeserialize(CurrentDraft(form)?.DraftPayload)),
                "REMS form resolved.")),

            // Draft / Saved: built but not yet sent — the link is not active until the Admin sends it.
            _ => Ok(ApiResponseFactory.Success(new RemsPublicFormResponse(RemsPublicFormStates.Unavailable), "REMS form resolved.")),
        };
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
                new RemsPublicFormResponse(RemsPublicFormStates.Submitted, ClientName: rems.RequestedClientName),
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
            new RemsPublicFormResponse(RemsPublicFormStates.Submitted, ClientName: effective!.ClientName?.Trim() ?? rems.RequestedClientName),
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
        var graph = BuildSubmitGraph(form, payload, now);

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
            foreach (var engagement in graph.Engagements)
            {
                await _engagements.AddAsync(engagement, ct);
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
            form.Rems!.Status = RemsRequestStatuses.EngagementSetup;

            // 8. Commit.
            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    /// <summary>
    /// Materialises the whole submit graph (submission, client, entities, addresses, contacts, engagements,
    /// government detail) with explicit TenantId on every REMS/Person row and pre-generated ids. Pure (no
    /// I/O) so it can run outside the transaction.
    /// </summary>
    private SubmitGraph BuildSubmitGraph(REMSForm form, RemsFormPayloadV1 payload, DateTime now)
    {
        var tenantId = form.TenantId;
        var isBusiness = string.Equals(form.IndustryGroup, RemsFormPayloadValidator.Business, StringComparison.Ordinal);
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

        // Client billing address (only when supplied). Email is LOCKED to the request — the payload email is
        // never read on submit.
        Guid? billingAddressId = null;
        if (payload.BillingAddress is { HasAny: true } billing)
        {
            var billingAddress = NewAddress(billing, AddressType.Billing);
            graph.Addresses.Add(billingAddress);
            billingAddressId = billingAddress.Id;
        }

        graph.Client = new REMSClient
        {
            Id = clientId,
            TenantId = tenantId,
            REMSId = form.REMSId,
            SourceFormSubmissionId = submissionId,
            Name = Clean(payload.ClientName) ?? string.Empty,
            Email = form.Rems!.CustomerEmail ?? string.Empty, // LOCKED to the request's customer email.
            MobileNumber = Clean(payload.MobileNumber),
            ReferralSource = Clean(payload.ReferralSource),
            BillingContactName = Clean(payload.BillingContactName),
            BillingEmail = Clean(payload.BillingEmail),
            BillingAddressId = billingAddressId,
        };

        // Main entity + its addresses, role contacts, blank engagement and (Government) contract detail.
        var mainEntityId = Guid.NewGuid();
        graph.Entities.Add(new REMSEntity
        {
            Id = mainEntityId,
            TenantId = tenantId,
            REMSClientId = clientId,
            SourceEntityKey = "main",
            Name = Clean(payload.ClientName) ?? string.Empty,
            EIN = isBusiness ? Clean(payload.Ein) : null,
            IsMainEntity = true,
        });
        StageEntityAddresses(graph, tenantId, mainEntityId, payload.PhysicalAddress, payload.MailingDiffers, payload.MailingAddress);
        StageRoleContacts(graph, tenantId, form.IndustryGroup, mainEntityId, payload.Roles);
        var mainEngagementId = StageBlankEngagement(graph, tenantId, mainEntityId);

        if (isGovernment)
        {
            graph.GovernmentDetail = new REMSEngagementGovernmentDetail
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                REMSEngagementId = mainEngagementId,
                ContractStartDate = payload.ContractStartDate,
                ContractEndDate = payload.ContractEndDate,
                OriginalTerm = Clean(payload.OriginalTerm),
                RenewalTerms = Clean(payload.RenewalTerms),
                PurchaseOrderStartDate = payload.PoStartDate,
                PurchaseOrderEndDate = payload.PoEndDate,
            };
        }

        // Related entities: one entity + (optional) addresses + a blank engagement each. They carry no role
        // contacts (the payload provides only a name, preserved verbatim in the immutable submission).
        var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "main" };
        var fallbackIndex = 0;
        foreach (var related in payload.RelatedEntities)
        {
            var relatedEntityId = Guid.NewGuid();
            graph.Entities.Add(new REMSEntity
            {
                Id = relatedEntityId,
                TenantId = tenantId,
                REMSClientId = clientId,
                SourceEntityKey = UniqueEntityKey(related.SourceKey, usedKeys, ref fallbackIndex),
                Name = Clean(related.BusinessName) ?? string.Empty,
                EIN = Clean(related.Ein),
                IsMainEntity = false,
            });
            StageEntityAddresses(
                graph, tenantId, relatedEntityId, related.PhysicalAddress,
                mailingDiffers: related.MailingAddress?.HasAny == true, related.MailingAddress);
            StageBlankEngagement(graph, tenantId, relatedEntityId);
        }

        return graph;
    }

    private static void StageEntityAddresses(
        SubmitGraph graph, Guid tenantId, Guid entityId, RemsAddressPayload? physical, bool mailingDiffers, RemsAddressPayload? mailing)
    {
        // Physical is always captured for the main entity (validation-required); optional for related entities.
        if (physical is { HasAny: true })
        {
            var address = NewAddress(physical, AddressType.Office);
            graph.Addresses.Add(address);
            graph.EntityAddresses.Add(NewEntityAddress(tenantId, entityId, address.Id, RemsAddressType.Physical));
        }

        if (mailingDiffers && mailing is { HasAny: true })
        {
            var address = NewAddress(mailing, AddressType.Other);
            graph.Addresses.Add(address);
            graph.EntityAddresses.Add(NewEntityAddress(tenantId, entityId, address.Id, RemsAddressType.Mailing));
        }
    }

    private static void StageRoleContacts(SubmitGraph graph, Guid tenantId, string industryGroup, Guid entityId, RemsRolesPayload? roles)
    {
        if (roles is null)
        {
            return;
        }

        foreach (var (role, roleName, isRequired) in EnumerateRoles(industryGroup, roles))
        {
            if (role is not { HasAny: true })
            {
                continue;
            }

            var person = BuildContactPerson(tenantId, role);
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
    /// </summary>
    private static Person BuildContactPerson(Guid tenantId, RemsRolePayload role)
    {
        var (first, last) = SplitName(role.Name);
        return new Person
        {
            Id = Guid.NewGuid(),
            PersonCode = "PER-" + Guid.NewGuid().ToString("N").ToUpperInvariant(),
            TenantId = tenantId,
            FirstName = first,
            LastName = last,
            DisplayName = Clean(role.Name) ?? first,
            PrimaryEmail = Clean(role.Email),
            MobileNumber = Clean(role.Phone),
            IsActive = true,
            LastProfileUpdatedOn = DateTime.UtcNow,
        };
    }

    private static Guid StageBlankEngagement(SubmitGraph graph, Guid tenantId, Guid entityId)
    {
        var engagement = new REMSEngagement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            REMSEntityId = entityId,
            Status = RemsEngagementStatus.Draft,
        };
        graph.Engagements.Add(engagement);
        return engagement.Id;
    }

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
        public List<REMSEngagement> Engagements { get; } = new();
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
        // raised the request is waiting on.
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
                    $"{rems.REMSNumber} — {rems.Title}",
                    EntityType.Rems,
                    rems.Id), cancellationToken);
            }

            await _activity.WriteAsync(
                new CreateActivityEventDto(EntityType.Rems, rems.Id, ActivityEventTypes.RemsFormSubmitted), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // External "form submitted" email to the Admin + CSE. Enqueued on a worker; never throws/blocks.
            var model = new RemsFormSubmittedEmail(
                Clean(payload.ClientName) ?? rems.RequestedClientName,
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
            Clean(payload.ClientName), rems.CustomerEmail ?? string.Empty, Clean(payload.MobileNumber), Clean(payload.ReferralSource));

        var contract = string.Equals(industryGroup, RemsFormPayloadValidator.Government, StringComparison.Ordinal)
            ? new RemsReviewContractDetails(
                payload.ContractStartDate, payload.ContractEndDate, Clean(payload.OriginalTerm),
                Clean(payload.RenewalTerms), payload.PoStartDate, payload.PoEndDate)
            : null;

        var others = payload.RelatedEntities
            .Select(r => new RemsReviewOtherEntity(
                Clean(r.SourceKey), Clean(r.BusinessName), Clean(r.Ein), Clean(r.ContactName),
                NonEmpty(r.PhysicalAddress), NonEmpty(r.MailingAddress)))
            .ToList();

        var address = new RemsReviewAddressGroup(
            NonEmpty(payload.PhysicalAddress), payload.MailingDiffers, payload.MailingDiffers ? NonEmpty(payload.MailingAddress) : null);

        var additionalContacts = EnumerateRoles(industryGroup, payload.Roles ?? new RemsRolesPayload())
            .Where(t => t.Role is { HasAny: true })
            .Select(t => new RemsReviewContactRow(t.RoleName, t.IsRequired, Clean(t.Role!.Name), Clean(t.Role.Email), Clean(t.Role.Phone)))
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
        switch (industryGroup)
        {
            case RemsFormPayloadValidator.Individual:
                yield return (roles.Self, nameof(RemsContactRole.Self), true);
                yield return (roles.Spouse, nameof(RemsContactRole.Spouse), false);
                break;

            case RemsFormPayloadValidator.Business:
                yield return (roles.Ceo, nameof(RemsContactRole.CEO), true);
                yield return (roles.Cfo, nameof(RemsContactRole.CFO), true);
                yield return (roles.AccountsPayable, nameof(RemsContactRole.AccountsPayable), true);
                yield return (roles.Banker, nameof(RemsContactRole.Banker), false);
                yield return (roles.Lawyer, nameof(RemsContactRole.Lawyer), false);
                break;

            case RemsFormPayloadValidator.Government:
                yield return (roles.FinanceDirector, nameof(RemsContactRole.FinanceDirector), true);
                yield return (roles.AccountsPayable, nameof(RemsContactRole.AccountsPayable), false);
                break;
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

    private static RemsAddressPayload? NonEmpty(RemsAddressPayload? address) => address is { HasAny: true } ? address : null;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private IActionResult FormNotFound()
        => NotFound(ApiResponseFactory.NotFound("This form is not available."));

    private IActionResult NotEditable()
        => StatusCode(StatusCodes.Status409Conflict, ApiResponseFactory.Error(
            CodeNotEditable, "This form is not available.", "The form is not currently accepting changes."));
}
