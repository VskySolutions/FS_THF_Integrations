using System.Security.Cryptography;
using System.Text.Json;
using EmsPortal.Api.Models.Rems;
using EmsPortal.Api.Security;
using EmsPortal.Application.Abstractions.Email;
using EmsPortal.Application.Abstractions.OptionSets;
using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Application.Abstractions.UniversalFeatures;
using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;
using EmsPortal.Shared.Configuration;
using EmsPortal.Shared.Contracts;
using EmsPortal.Shared.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace EmsPortal.Api.Controllers;

/// <summary>
/// REMS EMS-form build/send + email-log backend (WO-112). Admin operations on a request's customer-facing
/// onboarding form: build/save it (assign the CSE, pick the industry group, mint the invite link),
/// preview and send the form-link email, read the form's email-delivery log, and browse the EMS Inbox of
/// every request with a form. Endpoints are permission-gated (rems.forms.* / rems.pool.read /
/// rems.emailLog.read — Admin/Super Admin only); tenant isolation is ambient, so a request/form outside
/// the caller's tenant is simply a 404. Request-field editing on the build screen is done via WO-111's
/// <c>PUT /api/rems/requests/{id}</c>, not here.
/// </summary>
[ApiController]
[Route("api/rems/requests")]
[Produces("application/json")]
[Tags("REMS EMS Form")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status500InternalServerError)]
public sealed class RemsFormController : ControllerBase
{
    // REMS-form conflict codes (409): the form isn't in a state the requested action allows.
    private const string CodeFormNotBuilt = "REMS_FORM_NOT_BUILT";
    private const string CodeFormNotSendable = "REMS_FORM_NOT_SENDABLE";
    private const string CodeFormAlreadySent = "REMS_FORM_ALREADY_SENT";
    private const string CodeClientEmailMissing = "REMS_CLIENT_EMAIL_MISSING";
    private const string CodeCommissionNotFullyAllocated = "REMS_COMMISSION_NOT_FULLY_ALLOCATED";
    private const string CodeIndustryGroupLocked = "REMS_INDUSTRY_GROUP_LOCKED";
    private const string CodeFormAlreadySubmitted = "REMS_FORM_ALREADY_SUBMITTED";

    private readonly IRemsRepository _rems;
    private readonly IRemsFormRepository _forms;
    private readonly IRemsEngagementRepository _engagements;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IActivityEventWriter _activity;
    private readonly INotificationDispatcher _notifications;
    private readonly IRemsEmailNotifier _emailNotifier;
    private readonly IEmailTemplateService _templates;
    private readonly IOptionCodeResolver _codes;
    private readonly string _baseUrl;

    public RemsFormController(
        IRemsRepository rems,
        IRemsFormRepository forms,
        IRemsEngagementRepository engagements,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IActivityEventWriter activity,
        INotificationDispatcher notifications,
        IRemsEmailNotifier emailNotifier,
        IEmailTemplateService templates,
        IOptionCodeResolver codes,
        IOptions<AppOptions> appOptions)
    {
        _rems = rems;
        _forms = forms;
        _engagements = engagements;
        _users = users;
        _unitOfWork = unitOfWork;
        _activity = activity;
        _notifications = notifications;
        _emailNotifier = emailNotifier;
        _templates = templates;
        _codes = codes;
        _baseUrl = appOptions.Value.BaseUrl;
    }

    // -------------------- Build screen --------------------

    /// <summary>The EMS form build-screen model: the request context plus the current form (AC-REMS-007.1).</summary>
    /// <remarks>
    /// Open to the initiator (<c>rems.requests.update</c>) as well as to form managers: the CSE and the
    /// industry group are fields on their own request form now, not a separate admin build step.
    /// </remarks>
    [HttpGet("{remsId:guid}/form")]
    [RequireAnyPermission(Permissions.RemsFormsManage, Permissions.RemsRequestsUpdate)]
    [ProducesResponseType<ApiResponse<RemsFormBuildScreen>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetForm(Guid remsId, CancellationToken cancellationToken)
    {
        var rems = await _rems.GetByIdAsync(remsId, cancellationToken);
        if (rems is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS request not found."));
        }

        if (GuardSetupOwner(rems) is { } denied)
        {
            return denied;
        }

        var form = await _forms.GetByRemsIdAsync(remsId, cancellationToken);
        var screen = await BuildScreenAsync(rems, form, cancellationToken);
        return Ok(ApiResponseFactory.Success(screen, "REMS form retrieved."));
    }

    /// <summary>
    /// Build/save the form (AC-REMS-007): set the CSE on the request, create-or-update the form with the
    /// industry group, and mint (or, before send, regenerate) the invite link. Both CSE and industry group
    /// are required (AC-REMS-007.7). Once sent, the industry group and invite code are locked.
    /// </summary>
    [HttpPost("{remsId:guid}/form")]
    [RequireAnyPermission(Permissions.RemsFormsManage, Permissions.RemsRequestsUpdate)]
    [ProducesResponseType<ApiResponse<RemsFormBuildScreen>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveForm(Guid remsId, [FromBody] SaveRemsFormRequest request, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } me || User.GetActiveTenantId() is not { } tenantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponseFactory.Forbidden("No active tenant/user context."));
        }

        var rems = await _rems.GetByIdAsync(remsId, cancellationToken);
        if (rems is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS request not found."));
        }

        if (GuardSetupOwner(rems) is { } denied)
        {
            return denied;
        }

        // The CSE must resolve to a real user (AC-REMS-007.7).
        if (await _users.GetByIdAsync(request.CseUserId, cancellationToken) is null)
        {
            return BadRequest(ApiResponseFactory.Error(
                ApiErrorCodes.ValidationFailed, "Validation failed.", "Unknown cseUserId."));
        }

        var form = await _forms.GetByRemsIdAsync(remsId, cancellationToken);
        var alreadySent = form?.SentOnUtc is not null;
        var industryChanged = form is null
            || !string.Equals(form.IndustryGroup?.Value, request.IndustryGroup, StringComparison.Ordinal);

        // The entity type is a foreign key to its option item, so the CODE the caller sent is resolved once
        // here and used for both the create and the change below. Required, and locked once the form is
        // sent -- so an unknown code is a bad request rather than a null reference.
        var industryGroupId = await _codes.RequireRemsIdAsync(
            RemsOptionSetKeys.IndustryGroup, request.IndustryGroup, cancellationToken);

        // Once sent, the industry group / invite code are locked (AC-REMS-007.5).
        if (alreadySent && industryChanged)
        {
            return FormConflict(CodeIndustryGroupLocked,
                "The entity type and invite link are locked once the form has been sent.");
        }

        // CSE assign / reassign detection drives the in-app notification (AC-REMS-007.8/9).
        var previousCse = rems.CSEId;
        var notifyCse = previousCse != request.CseUserId; // first assignment (null -> x) or reassignment (x -> y)

        rems.CSEId = request.CseUserId;
        _rems.Update(rems);

        if (form is null)
        {
            // First save: CSE + industry group are present, so the form lands straight in Saved.
            form = new REMSForm
            {
                Id = Guid.NewGuid(),
                REMSId = remsId,
                IndustryGroupId = industryGroupId,
                InviteCode = await GenerateUniqueInviteCodeAsync(tenantId, cancellationToken),
                Status = RemsFormStatus.Saved,
                CreatedByUserId = me,
            };
            await _forms.AddAsync(form, cancellationToken);
        }
        else
        {
            // Changing the industry group before send regenerates the invite code/link (AC-REMS-007.5).
            if (industryChanged)
            {
                form.IndustryGroupId = industryGroupId;
                form.InviteCode = await GenerateUniqueInviteCodeAsync(tenantId, cancellationToken);
            }
            if (form.Status == RemsFormStatus.Draft)
            {
                form.Status = RemsFormStatus.Saved;
            }
            _forms.Update(form);
        }

        await _activity.WriteAsync(new CreateActivityEventDto(EntityType.Rems, remsId, ActivityEventTypes.RemsFormBuilt), cancellationToken);
        if (notifyCse)
        {
            await _notifications.DispatchAsync(CseAssignedNotification(request.CseUserId, rems), cancellationToken);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var refreshed = await _forms.GetByRemsIdAsync(remsId, cancellationToken) ?? form;
        var screen = await BuildScreenAsync(rems, refreshed, cancellationToken);
        return Ok(ApiResponseFactory.Success(screen, "REMS form saved."));
    }

    // -------------------- Preview & send --------------------

    /// <summary>The pre-send preview: destination email + form link (AC-REMS-008.1).</summary>
    [HttpGet("{remsId:guid}/form/preview")]
    [RequirePermission(Permissions.RemsFormsSend)]
    [ProducesResponseType<ApiResponse<RemsFormPreview>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Preview(Guid remsId, CancellationToken cancellationToken)
    {
        var rems = await _rems.GetByIdAsync(remsId, cancellationToken);
        if (rems is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS request not found."));
        }

        if (GuardSetupOwner(rems) is { } denied)
        {
            return denied;
        }

        var form = await _forms.GetByRemsIdAsync(remsId, cancellationToken);
        if (form is null)
        {
            return FormConflict(CodeFormNotBuilt, "The form has not been built yet.");
        }

        // Render the effective template with the values this send would use, so the dialog shows the real
        // email rather than a description of one — and so what the admin edits starts from what would
        // otherwise go out. Rendered here, not in the browser: the template is per-tenant and its
        // placeholders are the server's to substitute.
        var formLink = BuildFormLink(form.InviteCode);
        var rendered = User.GetActiveTenantId() is { } previewTenantId
            ? await _templates.RenderEffectiveAsync(
                previewTenantId,
                EmailTemplateKey.RemsFormLink,
                FormLinkModel(rems, formLink),
                cancellationToken)
            : null;

        var preview = new RemsFormPreview(
            Normalize(rems.CustomerEmail), formLink, rendered?.Subject, rendered?.Body);
        return Ok(ApiResponseFactory.Success(preview, "REMS form preview retrieved."));
    }

    /// <summary>
    /// Send the form-link email to the client (AC-REMS-008). Allowed only when the form is saved with a CSE
    /// and industry group and the client has an email; otherwise 409 and nothing is sent (AC-REMS-007.10 /
    /// 008.2). Sets the form to Sent, locks the invite code, records a <see cref="REMSFormEmailEvent"/>
    /// (Sent), and notifies the Admin actor + CSE (AC-REMS-008.5).
    /// </summary>
    [HttpPost("{remsId:guid}/form/send")]
    [RequirePermission(Permissions.RemsFormsSend)]
    [ProducesResponseType<ApiResponse<RemsFormBuildScreen>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Send(Guid remsId, [FromBody] SendRemsFormRequest? request, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } me || User.GetActiveTenantId() is not { } tenantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponseFactory.Forbidden("No active tenant/user context."));
        }

        var rems = await _rems.GetByIdAsync(remsId, cancellationToken);
        if (rems is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS request not found."));
        }

        if (GuardSetupOwner(rems) is { } denied)
        {
            return denied;
        }

        var form = await _forms.GetByRemsIdAsync(remsId, cancellationToken);
        if (form is null)
        {
            return FormConflict(CodeFormNotBuilt, "The form has not been built yet.");
        }
        if (form.SentOnUtc is not null || form.Status == RemsFormStatus.Sent)
        {
            return FormConflict(CodeFormAlreadySent, "The form has already been sent.");
        }
        // Must be saved with a CSE + industry group (AC-REMS-007.10).
        if (rems.CSEId is null || form.IndustryGroupId == Guid.Empty || form.Status != RemsFormStatus.Saved)
        {
            return FormConflict(CodeFormNotSendable,
                "The form must be saved with a CSE and an entity type before it can be sent.");
        }
        // The client must have an email to receive the link (AC-REMS-008.2).
        var email = Normalize(rems.CustomerEmail);
        if (email is null)
        {
            return FormConflict(CodeClientEmailMissing,
                "The client has no email address on file; add one before sending.");
        }

        // The commission has to be settled before the client is written to. The splits divide ONE
        // commission, so anything other than 100% is a share allocated to nobody — and every recipient
        // becomes a required approver on the round that follows, so a division that does not add up is one
        // the approvers would be asked to accept later, on a request already out with the client.
        //
        // Rounded to 2dp before comparing, as the Commission tab does: three 33.33/33.34 splits sum to
        // 100.00000000000001 in binary floating point and would otherwise never be sendable.
        var engagement = await _engagements.GetByRemsIdAsync(remsId, cancellationToken);
        var splits = engagement?.CommissionSplits.Where(s => !s.Deleted).ToList() ?? [];
        var allocated = Math.Round(splits.Sum(s => s.CommissionPercentage), 2, MidpointRounding.AwayFromZero);
        if (allocated != 100m)
        {
            // Naming nobody is its own sentence. An empty split is NOT "no commission on this one" — it is
            // a commission that has not been settled yet, and the message says so rather than pointing the
            // reader at recipients that do not exist.
            return FormConflict(
                CodeCommissionNotFullyAllocated,
                splits.Count == 0
                    ? "No commission recipients yet — the Commission tab must name recipients adding up "
                      + "to 100% before this request can be sent to the client."
                    : $"Commission totals {allocated:0.##}% — the recipients on the Commission tab must add up "
                      + "to 100% before this request can be sent to the client.");
        }

        var now = DateTime.UtcNow;
        var formLink = BuildFormLink(form.InviteCode);

        // Mint a stable outbound Message-ID and store it as the ProviderMessageId on the Sent event so a
        // delivery provider can echo it back on delivery/open/failed callbacks that WO-121 ingests. The same
        // id is threaded to the email pipeline and pinned on the MimeMessage.
        var messageId = BuildOutboundMessageId();

        form.Status = RemsFormStatus.Sent;
        form.SentOnUtc = now;
        form.InviteLockedOnUtc = now;
        _forms.Update(form);

        // Sending the intake link is what takes a request out of draft — there is no pool in between any
        // more, so this is the initiator's own hand-off to the client. Guarded on Draft so a request that
        // has already moved further along is never walked backwards by a re-send.
        if (rems.Status!.Value == RemsRequestStatuses.Draft)
        {
            rems.StatusId = await _codes.RequireRemsIdAsync(
                RemsOptionSetKeys.Status, RemsRequestStatuses.AwaitingCustomer, cancellationToken);
            _rems.Update(rems);
        }

        // Record the Sent delivery event (later delivery/open/failed events are ingested by WO-121, matched
        // to this row by ProviderMessageId).
        await _forms.AddEmailEventAsync(new REMSFormEmailEvent
        {
            Id = Guid.NewGuid(),
            REMSFormId = form.Id,
            ProviderMessageId = messageId,
            EventType = RemsFormEmailEventType.Sent,
            RecipientEmail = email,
            OccurredOnUtc = now,
            // What the client is about to read, kept alongside the fact that we sent it. The dialog seeds
            // these from the tenant's template and the sender may rewrite either, so the template is not a
            // record of what went out — this is.
            Subject = Normalize(request?.Subject),
            Body = Normalize(request?.Body),
        }, cancellationToken);

        await _activity.WriteAsync(new CreateActivityEventDto(EntityType.Rems, remsId, ActivityEventTypes.RemsFormSent), cancellationToken);
        // Sender, CSE, and whoever raised the request — the requester is tracking their client's onboarding
        // and this is the step that puts the ball in the customer's court.
        foreach (var recipient in new[] { me, rems.CSEId, rems.CreatedById }
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct())
        {
            await _notifications.DispatchAsync(FormSentNotification(recipient, rems), cancellationToken);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Enqueue the email only after the Sent state is durably persisted. Delivery is best-effort on a
        // Hangfire worker and never throws — a send failure must not roll the request back.
        // Whatever the admin left in the dialog wins over the template; leaving both untouched sends the
        // template exactly as the preview showed it.
        _emailNotifier.SendComposedFormLink(
            tenantId, email, new RemsFormLinkEmail(rems.ClientDisplayName, formLink, rems.REMSNumber),
            request?.Subject, request?.Body, messageId);

        var refreshed = await _forms.GetByRemsIdAsync(remsId, cancellationToken) ?? form;
        var screen = await BuildScreenAsync(rems, refreshed, cancellationToken);
        return Ok(ApiResponseFactory.Success(screen, "REMS form sent."));
    }

    // -------------------- Reminder --------------------

    /// <summary>
    /// The pre-send preview for a REMINDER, rendered from the <c>RemsFormReminder</c> template. Same shape
    /// as <see cref="Preview"/> so the send dialog can drive both.
    /// </summary>
    [HttpGet("{remsId:guid}/form/reminder/preview")]
    [RequirePermission(Permissions.RemsFormsSend)]
    [ProducesResponseType<ApiResponse<RemsFormPreview>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReminderPreview(Guid remsId, CancellationToken cancellationToken)
    {
        var context = await LoadRemindableAsync(remsId, cancellationToken);
        if (context.Failure is not null)
        {
            return context.Failure;
        }

        var (rems, form) = (context.Rems!, context.Form!);
        var formLink = BuildFormLink(form.InviteCode);
        var rendered = User.GetActiveTenantId() is { } tenantId
            ? await _templates.RenderEffectiveAsync(
                tenantId, EmailTemplateKey.RemsFormReminder, FormLinkModel(rems, formLink), cancellationToken)
            : null;

        var preview = new RemsFormPreview(
            Normalize(rems.CustomerEmail), formLink, rendered?.Subject, rendered?.Body);
        return Ok(ApiResponseFactory.Success(preview, "REMS form reminder preview retrieved."));
    }

    /// <summary>
    /// Re-send the form link to a client who has not submitted yet. Repeatable by design — chasing a
    /// client is a thing you do more than once — so unlike <see cref="Send"/> this changes no state on the
    /// form: the invite code, the Sent timestamp and the request status all stay exactly as they were.
    /// What it leaves behind is a <see cref="RemsFormEmailEventType.Reminder"/> row, so the email log
    /// answers "how many times have we chased them, and when".
    /// </summary>
    [HttpPost("{remsId:guid}/form/reminder")]
    [RequirePermission(Permissions.RemsFormsSend)]
    [ProducesResponseType<ApiResponse<RemsFormBuildScreen>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SendReminder(
        Guid remsId, [FromBody] SendRemsFormRequest? request, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } me || User.GetActiveTenantId() is not { } tenantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponseFactory.Forbidden("No active tenant/user context."));
        }

        var context = await LoadRemindableAsync(remsId, cancellationToken);
        if (context.Failure is not null)
        {
            return context.Failure;
        }

        var (rems, form) = (context.Rems!, context.Form!);
        var email = Normalize(rems.CustomerEmail)!; // guaranteed by LoadRemindableAsync
        var formLink = BuildFormLink(form.InviteCode);
        var messageId = BuildOutboundMessageId();

        await _forms.AddEmailEventAsync(new REMSFormEmailEvent
        {
            Id = Guid.NewGuid(),
            REMSFormId = form.Id,
            ProviderMessageId = messageId,
            EventType = RemsFormEmailEventType.Reminder,
            RecipientEmail = email,
            OccurredOnUtc = DateTime.UtcNow,
            Subject = Normalize(request?.Subject),
            Body = Normalize(request?.Body),
        }, cancellationToken);

        await _activity.WriteAsync(
            new CreateActivityEventDto(EntityType.Rems, remsId, ActivityEventTypes.RemsFormReminderSent), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Enqueued after the event row is durable, as the first send does. Best-effort on a Hangfire
        // worker; a delivery failure must not roll back the record that we tried.
        _emailNotifier.SendComposedFormReminder(
            tenantId, email, new RemsFormLinkEmail(rems.ClientDisplayName, formLink, rems.REMSNumber),
            request?.Subject, request?.Body, messageId);

        // Nobody in-app is notified: this is the admin chasing the client, and telling the team each time
        // somebody clicks Remind would be noise about their own action.
        _ = me;

        var refreshed = await _forms.GetByRemsIdAsync(remsId, cancellationToken) ?? form;
        var screen = await BuildScreenAsync(rems, refreshed, cancellationToken);
        return Ok(ApiResponseFactory.Success(screen, "Reminder sent."));
    }

    /// <summary>
    /// Resolves a request whose client can legitimately be reminded, or the error explaining why not.
    /// </summary>
    private async Task<(REMS? Rems, REMSForm? Form, IActionResult? Failure)> LoadRemindableAsync(
        Guid remsId, CancellationToken cancellationToken)
    {
        var rems = await _rems.GetByIdAsync(remsId, cancellationToken);
        if (rems is null)
        {
            return (null, null, NotFound(ApiResponseFactory.NotFound("REMS request not found.")));
        }

        if (GuardSetupOwner(rems) is { } denied)
        {
            return (null, null, denied);
        }

        var form = await _forms.GetByRemsIdAsync(remsId, cancellationToken);
        if (RemindBlocked(rems, form) is { } blocked)
        {
            return (null, null, FormConflict(blocked.Code, blocked.Reason));
        }

        return (rems, form, null);
    }

    /// <summary>
    /// Why this request's client cannot be reminded right now, or null when they can. A reminder makes
    /// sense in exactly one window: the form has been SENT and the client has not submitted. Before that
    /// there is nothing to remind them about; after it, nothing to chase.
    /// <para>
    /// One definition, read by both the endpoint that sends a reminder and the email log that offers the
    /// button — so the log never shows a Remind the request would refuse, and never hides one it would
    /// have allowed. Says nothing about WHO is asking; that is the caller's permission and
    /// <see cref="RemsSetupAccess"/> record rule, checked alongside it.
    /// </para>
    /// </summary>
    private static (string Code, string Reason)? RemindBlocked(REMS rems, REMSForm? form)
    {
        if (form is null)
        {
            return (CodeFormNotBuilt, "The form has not been built yet.");
        }
        if (form.SentOnUtc is null)
        {
            return (CodeFormNotSendable, "The form has not been sent yet, so there is nothing to remind the client about.");
        }
        if (form.Status == RemsFormStatus.Submitted)
        {
            return (CodeFormAlreadySubmitted, "The client has already submitted this form.");
        }
        if (form.Status == RemsFormStatus.Cancelled)
        {
            return (CodeFormNotSendable, "This form has been cancelled.");
        }
        if (Normalize(rems.CustomerEmail) is null)
        {
            return (CodeClientEmailMissing, "The client has no email address on file; add one before sending.");
        }

        return null;
    }

    // -------------------- Email log --------------------

    /// <summary>
    /// The request's email history, newest first (AC-REMS-008.6), together with whether this caller can
    /// nudge the client from it. Every send and every provider callback for the intake form lands here,
    /// so it is the answer to "did they get it, and how many times have we asked".
    /// <para>
    /// Gated twice: <c>rems.emailLog.read</c> says the caller may read logs at all, and
    /// <see cref="RemsSetupAccess.CanRead"/> says they may read THIS one — the request's initiator, its
    /// CSE, the admin reviewing it, or anyone who manages engagements. Who a client is being chased by
    /// is not tenant-wide reading material.
    /// </para>
    /// </summary>
    [HttpGet("{remsId:guid}/email-log")]
    [RequirePermission(Permissions.RemsEmailLogRead)]
    [ProducesResponseType<ApiResponse<RemsEmailLog>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> EmailLog(Guid remsId, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } me)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        var rems = await _rems.GetByIdAsync(remsId, cancellationToken);
        if (rems is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS request not found."));
        }

        if (!RemsSetupAccess.CanRead(User, rems, me))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponseFactory.Forbidden("This request's email log is only open to the people named on it."));
        }

        var form = await _forms.GetByRemsIdAsync(remsId, cancellationToken);
        var events = form is null
            ? Array.Empty<REMSFormEmailEvent>()
            : await _forms.ListEmailEventsAsync(form.Id, cancellationToken);

        // Who pressed Send / Remind. Resolved in one lookup for the whole page rather than per row, and
        // only over the events that name somebody — provider callbacks carry no actor.
        var senders = await _users.GetFullNamesAsync(
            events.Select(e => e.CreatedById).Where(id => id.HasValue).Select(id => id!.Value).Distinct(),
            cancellationToken);

        var rows = events.Select(e => new RemsEmailEventRow(
            e.Id, e.EventType.ToString(), e.RecipientEmail, e.OccurredOnUtc,
            DescribeFailure(e),
            e.CreatedById is { } actor && senders.TryGetValue(actor, out var name) ? name : null,
            e.Subject, e.Body)).ToList();

        // Exactly what POST .../form/reminder would decide, asked ahead of the click.
        var blocked = RemindBlocked(rems, form);
        var maySend = User.HasPermission(Permissions.RemsFormsSend);
        var isOwner = RemsSetupAccess.CanWork(User, rems, me);
        var reason = !maySend
            ? null
            : blocked?.Reason ?? (isOwner ? null : RemsSetupAccess.WorkDeniedReason(rems));

        // The client's link, only while it is genuinely theirs to follow: sent, and not yet answered. The
        // same window a reminder makes sense in, minus the permission and record checks above — copying a
        // link to chase somebody with is not the same act as sending mail on the firm's behalf, so anyone
        // who may READ this log may copy it.
        var linkable = form is not null
            && !string.IsNullOrWhiteSpace(form.InviteCode)
            && form.SentOnUtc is not null
            && form.Status is not (RemsFormStatus.Submitted or RemsFormStatus.Cancelled);
        var clientFormLink = linkable ? BuildFormLink(form!.InviteCode) : null;

        var log = new RemsEmailLog(blocked is null && maySend && isOwner, reason, rows, clientFormLink);
        return Ok(ApiResponseFactory.Success(log, "REMS form email log retrieved."));
    }

    // -------------------- EMS Inbox --------------------

    /// <summary>
    /// The EMS Inbox: every request that has a form, paginated and newest-modified first, with request
    /// context, form state, creator, and latest send/delivery/open info (AC-REMS-009). Narrow it with
    /// <paramref name="formState"/> (draft/saved/sent/submitted/cancelled), <paramref name="requestStatus"/>,
    /// and <paramref name="search"/> over the REMS number and client name. Filtering is server-side so the
    /// pager reports the filtered total rather than the page in hand.
    /// </summary>
    [HttpGet("/api/rems/inbox")]
    [RequireAnyPermission(Permissions.RemsRequestsRead, Permissions.RemsFormsManage)]
    [ProducesResponseType<ApiResponse<IEnumerable<RemsInboxRow>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Inbox(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string? formState = null,
        [FromQuery] string? search = null,
        [FromQuery] string? requestStatus = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);

        RemsFormStatus? state = Enum.TryParse<RemsFormStatus>(formState, ignoreCase: true, out var parsed) ? parsed : null;

        var (items, total) = await _forms.ListInboxAsync(
            new RemsInboxQuery(state, search, requestStatus, page, limit), cancellationToken);
        var names = await _users.GetFullNamesAsync(
            items.Select(i => i.FormCreatedByUserId)
                .Concat(items.SelectMany(i => new[] { i.CreatedById, i.UpdatedById, i.AdminAssignedToId })
                    .Where(id => id.HasValue).Select(id => id!.Value)),
            cancellationToken);

        string? NameOf(Guid? id) => id is { } uid && names.TryGetValue(uid, out var name) ? name : null;

        var rows = items.Select(i => new RemsInboxRow(
            i.RemsId, i.RemsNumber, i.ClientName, i.EngagementType, i.RequestStatus,
            i.FormStatus.ToString(),
            new RemsUserRef(i.FormCreatedByUserId, names.TryGetValue(i.FormCreatedByUserId, out var n) ? n : string.Empty),
            i.FormSentOnUtc,
            i.LatestEmailEventType?.ToString(),
            i.LatestEmailEventOnUtc,
            RemsWorkspaceMapper.UserRef(i.AdminAssignedToId, names),
            NameOf(i.CreatedById), i.CreatedOnUtc, NameOf(i.UpdatedById), i.UpdatedOnUtc));

        return Ok(ApiResponseFactory.Paginated(rows, "REMS EMS inbox retrieved.", page, limit, total));
    }

    // -------------------- Helpers --------------------

    /// <summary>
    /// The refusal for reading or writing this request's form, or null to carry on. The CSE and the
    /// industry group are part of the engagement setup, so they follow the same record rule as the rest of
    /// it (<see cref="RemsSetupAccess"/>): whoever the request is with at this stage may set them, and
    /// holding a REMS permission is not on its own an answer to "which requests".
    /// </summary>
    private IActionResult? GuardSetupOwner(REMS rems)
    {
        if (User.GetUserId() is not { } me)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        return RemsSetupAccess.CanWork(User, rems, me)
            ? null
            : StatusCode(StatusCodes.Status403Forbidden,
                ApiResponseFactory.Forbidden(RemsSetupAccess.WorkDeniedReason(rems)));
    }

    /// <summary>
    /// The build screen for a request and its form.
    /// <para>
    /// The two option codes are resolved from the FOREIGN KEYS, not read off the <c>Status</c> /
    /// <c>IndustryGroup</c> navigations, and that is load-bearing rather than a matter of taste. This helper
    /// is called after the write in every action that has one, and <see cref="Send"/> moves the request from
    /// Draft to Awaiting Customer on its way here. Changing <c>rems.StatusId</c> makes EF's fixup null
    /// <c>rems.Status</c> — the navigation no longer matches the key, and the new option item is never
    /// tracked because <see cref="IOptionCodeResolver"/> answers from a cache and returns bare guids — so
    /// the caller arrived here holding a request whose status navigation had been detached under it.
    /// <c>rems.Status!.Value</c> then threw a NullReferenceException on a send that had already gone out:
    /// the row was written, the email was queued, and the client got their link, but the caller saw a 500.
    /// The <c>!</c> was no protection at all, being a note to the compiler rather than a check.
    /// </para>
    /// <para>
    /// Reading the ids costs nothing extra — the resolver is a cached id → code lookup — and it is the same
    /// answer, from the value the row actually holds rather than from whatever EF happens to have loaded.
    /// </para>
    /// </summary>
    private async Task<RemsFormBuildScreen> BuildScreenAsync(REMS rems, REMSForm? form, CancellationToken cancellationToken)
    {
        var names = await _users.GetFullNamesAsync(
            rems.CSEId is { } cse ? new[] { cse } : Array.Empty<Guid>(), cancellationToken);

        var cseRef = rems.CSEId is { } cseId
            ? new RemsUserRef(cseId, names.TryGetValue(cseId, out var name) ? name : string.Empty)
            : null;

        // One lookup for both codes rather than one each.
        var codes = await _codes.CodesOfAsync(
            new Guid?[] { rems.StatusId, form?.IndustryGroupId }, cancellationToken);
        string CodeOf(Guid? id) => id is { } key && codes.TryGetValue(key, out var code) ? code : string.Empty;

        var formInfo = form is null
            ? null
            : new RemsFormInfo(
                form.Id,
                CodeOf(form.IndustryGroupId),
                form.InviteCode,
                BuildFormLink(form.InviteCode),
                form.Status.ToString(),
                form.SentOnUtc,
                form.SubmittedOnUtc,
                form.InviteLockedOnUtc,
                form.SentOnUtc is not null);

        return new RemsFormBuildScreen(
            rems.Id, rems.REMSNumber, rems.ClientDisplayName, CodeOf(rems.StatusId),
            rems.CustomerEmail, rems.CustomerMobileNumber, cseRef, formInfo);
    }

    /// <summary>The public EMS-form URL (the route WO-113/116 serve), built from <c>App:BaseUrl</c> as the email pipeline does.</summary>
    private string BuildFormLink(string inviteCode)
        => $"{_baseUrl.TrimEnd('/')}/rems/form/{inviteCode}";

    /// <summary>
    /// A stable, RFC-5322-style Message-ID (<c>{guid:N}@{host}</c>, no angle brackets) minted at send time
    /// (WO-121). Stored as the Sent event's <c>ProviderMessageId</c> and pinned on the outbound email so a
    /// provider's delivery/open/failed callbacks correlate back to this request. Host derives from
    /// <c>App:BaseUrl</c>, falling back to a placeholder when it is not an absolute URL.
    /// </summary>
    /// <summary>
    /// The placeholder values the <c>RemsFormLink</c> template renders with. One definition, so the preview
    /// the admin edits is rendered from exactly what the send would have used.
    /// </summary>
    private static Dictionary<string, string?> FormLinkModel(REMS rems, string formLink) => new(StringComparer.OrdinalIgnoreCase)
    {
        ["ClientName"] = rems.ClientDisplayName,
        ["FormLink"] = formLink,
        ["RemsNumber"] = rems.REMSNumber,
    };

    private string BuildOutboundMessageId()
    {
        var host = Uri.TryCreate(_baseUrl, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host)
            ? uri.Host
            : "emsportal.local";
        return $"{Guid.NewGuid():N}@{host}";
    }

    /// <summary>
    /// Mints an opaque, URL-safe invite code (22 chars, 128 bits) unique per tenant. Uniqueness is backed
    /// by the filtered unique index <c>(TenantId, InviteCode) WHERE [Deleted] = 0</c>; this retry loop only
    /// avoids the (astronomically unlikely) in-flight collision.
    /// </summary>
    private async Task<string> GenerateUniqueInviteCodeAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = GenerateInviteCode();
            if (!await _forms.InviteCodeExistsAsync(tenantId, code, cancellationToken))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique REMS invite code.");
    }

    private static string GenerateInviteCode()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        // base64url, no padding — 16 bytes -> 22 URL-safe chars.
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static CreateNotificationDto CseAssignedNotification(Guid recipientId, REMS rems)
        => new(
            recipientId,
            NotificationType.RemsCseAssigned,
            "You were assigned as CSE on a REMS request",
            $"{rems.REMSNumber} — {rems.ClientDisplayName}",
            EntityType.Rems,
            rems.Id);

    private static CreateNotificationDto FormSentNotification(Guid recipientId, REMS rems)
        => new(
            recipientId,
            NotificationType.RemsFormSent,
            "A REMS onboarding form was sent to the client",
            $"{rems.REMSNumber} — {rems.ClientDisplayName}",
            EntityType.Rems,
            rems.Id);

    private IActionResult FormConflict(string code, string message)
        => StatusCode(StatusCodes.Status409Conflict, ApiResponseFactory.Error(code, message, message));

    /// <summary>
    /// The human-readable reason for a Failed event, for the Email Log. Only events this portal recorded
    /// itself carry one — <c>RemsEmailDeliveryFailureSink</c> tags its payload <c>"source":"portal"</c> and
    /// puts the reason in <c>message</c>. A provider webhook payload is third-party JSON of unknown shape,
    /// so it is deliberately never echoed to the UI.
    /// </summary>
    private static string? DescribeFailure(REMSFormEmailEvent emailEvent)
    {
        if (emailEvent.EventType != RemsFormEmailEventType.Failed
            || string.IsNullOrWhiteSpace(emailEvent.ProviderPayload))
        {
            return null;
        }

        try
        {
            using var payload = JsonDocument.Parse(emailEvent.ProviderPayload);
            if (payload.RootElement.ValueKind is not JsonValueKind.Object
                || !payload.RootElement.TryGetProperty("source", out var source)
                || source.GetString() != "portal")
            {
                return null;
            }

            return payload.RootElement.TryGetProperty("message", out var message)
                ? Normalize(message.GetString())
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
