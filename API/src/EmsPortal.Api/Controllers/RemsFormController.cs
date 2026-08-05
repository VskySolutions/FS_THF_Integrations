using System.Security.Cryptography;
using System.Text.Json;
using EmsPortal.Api.Models.Rems;
using EmsPortal.Api.Security;
using EmsPortal.Application.Abstractions.Email;
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
    private const string CodeIndustryGroupLocked = "REMS_INDUSTRY_GROUP_LOCKED";

    private readonly IRemsRepository _rems;
    private readonly IRemsFormRepository _forms;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IActivityEventWriter _activity;
    private readonly INotificationDispatcher _notifications;
    private readonly IRemsEmailNotifier _emailNotifier;
    private readonly string _baseUrl;

    public RemsFormController(
        IRemsRepository rems,
        IRemsFormRepository forms,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IActivityEventWriter activity,
        INotificationDispatcher notifications,
        IRemsEmailNotifier emailNotifier,
        IOptions<AppOptions> appOptions)
    {
        _rems = rems;
        _forms = forms;
        _users = users;
        _unitOfWork = unitOfWork;
        _activity = activity;
        _notifications = notifications;
        _emailNotifier = emailNotifier;
        _baseUrl = appOptions.Value.BaseUrl;
    }

    // -------------------- Build screen --------------------

    /// <summary>The EMS form build-screen model: the request context plus the current form (AC-REMS-007.1).</summary>
    [HttpGet("{remsId:guid}/form")]
    [RequirePermission(Permissions.RemsFormsManage)]
    [ProducesResponseType<ApiResponse<RemsFormBuildScreen>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetForm(Guid remsId, CancellationToken cancellationToken)
    {
        var rems = await _rems.GetByIdAsync(remsId, cancellationToken);
        if (rems is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS request not found."));
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
    [RequirePermission(Permissions.RemsFormsManage)]
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

        // The CSE must resolve to a real user (AC-REMS-007.7).
        if (await _users.GetByIdAsync(request.CseUserId, cancellationToken) is null)
        {
            return BadRequest(ApiResponseFactory.Error(
                ApiErrorCodes.ValidationFailed, "Validation failed.", "Unknown cseUserId."));
        }

        var form = await _forms.GetByRemsIdAsync(remsId, cancellationToken);
        var alreadySent = form?.SentOnUtc is not null;
        var industryChanged = form is null || !string.Equals(form.IndustryGroup, request.IndustryGroup, StringComparison.Ordinal);

        // Once sent, the industry group / invite code are locked (AC-REMS-007.5).
        if (alreadySent && industryChanged)
        {
            return FormConflict(CodeIndustryGroupLocked,
                "The industry group and invite link are locked once the form has been sent.");
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
                IndustryGroup = request.IndustryGroup,
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
                form.IndustryGroup = request.IndustryGroup;
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

        var form = await _forms.GetByRemsIdAsync(remsId, cancellationToken);
        if (form is null)
        {
            return FormConflict(CodeFormNotBuilt, "The form has not been built yet.");
        }

        var preview = new RemsFormPreview(Normalize(rems.CustomerEmail), BuildFormLink(form.InviteCode));
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
    public async Task<IActionResult> Send(Guid remsId, CancellationToken cancellationToken)
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
        if (rems.CSEId is null || string.IsNullOrWhiteSpace(form.IndustryGroup) || form.Status != RemsFormStatus.Saved)
        {
            return FormConflict(CodeFormNotSendable,
                "The form must be saved with a CSE and an industry group before it can be sent.");
        }
        // The client must have an email to receive the link (AC-REMS-008.2).
        var email = Normalize(rems.CustomerEmail);
        if (email is null)
        {
            return FormConflict(CodeClientEmailMissing,
                "The client has no email address on file; add one before sending.");
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

        // The request now waits on the client, so say so — leaving it on "Submitted" reads as still sitting
        // in the Admin Pool. Guarded on the pool status so a request that has somehow moved further along
        // (or was never in the pool) is never walked backwards.
        if (rems.Status == RemsRequestStatuses.Submitted)
        {
            rems.Status = RemsRequestStatuses.AwaitingCustomer;
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
        _emailNotifier.SendFormLink(
            tenantId, email, new RemsFormLinkEmail(rems.RequestedClientName, formLink, rems.REMSNumber, rems.Title), messageId);

        var refreshed = await _forms.GetByRemsIdAsync(remsId, cancellationToken) ?? form;
        var screen = await BuildScreenAsync(rems, refreshed, cancellationToken);
        return Ok(ApiResponseFactory.Success(screen, "REMS form sent."));
    }

    // -------------------- Email log --------------------

    /// <summary>The form's email-delivery events, newest first (AC-REMS-008.6).</summary>
    [HttpGet("{remsId:guid}/email-log")]
    [RequirePermission(Permissions.RemsEmailLogRead)]
    [ProducesResponseType<ApiResponse<IEnumerable<RemsEmailEventRow>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> EmailLog(Guid remsId, CancellationToken cancellationToken)
    {
        var rems = await _rems.GetByIdAsync(remsId, cancellationToken);
        if (rems is null)
        {
            return NotFound(ApiResponseFactory.NotFound("REMS request not found."));
        }

        var form = await _forms.GetByRemsIdAsync(remsId, cancellationToken);
        if (form is null)
        {
            return Ok(ApiResponseFactory.Success(Array.Empty<RemsEmailEventRow>(), "No form has been built yet."));
        }

        var events = await _forms.ListEmailEventsAsync(form.Id, cancellationToken);
        var rows = events.Select(e => new RemsEmailEventRow(
            e.Id, e.EventType.ToString(), e.RecipientEmail, e.OccurredOnUtc, e.ProviderMessageId, DescribeFailure(e)));
        return Ok(ApiResponseFactory.Success(rows, "REMS form email log retrieved."));
    }

    // -------------------- EMS Inbox --------------------

    /// <summary>
    /// The EMS Inbox: every request that has a form, paginated and newest-modified first, with request
    /// context, form state, creator, and latest send/delivery/open info (AC-REMS-009). Filter by form
    /// state via <paramref name="formState"/> (draft/saved/sent/submitted/cancelled).
    /// </summary>
    [HttpGet("/api/rems/inbox")]
    [RequireAnyPermission(Permissions.RemsPoolRead, Permissions.RemsFormsManage)]
    [ProducesResponseType<ApiResponse<IEnumerable<RemsInboxRow>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Inbox(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string? formState = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);

        RemsFormStatus? state = Enum.TryParse<RemsFormStatus>(formState, ignoreCase: true, out var parsed) ? parsed : null;

        var (items, total) = await _forms.ListInboxAsync(new RemsInboxQuery(state, page, limit), cancellationToken);
        var names = await _users.GetFullNamesAsync(items.Select(i => i.FormCreatedByUserId), cancellationToken);

        var rows = items.Select(i => new RemsInboxRow(
            i.RemsId, i.RemsNumber, i.ClientName, i.EngagementType, i.RequestStatus,
            i.FormStatus.ToString(),
            new RemsUserRef(i.FormCreatedByUserId, names.TryGetValue(i.FormCreatedByUserId, out var n) ? n : string.Empty),
            i.FormSentOnUtc,
            i.LatestEmailEventType?.ToString(),
            i.LatestEmailEventOnUtc));

        return Ok(ApiResponseFactory.Paginated(rows, "REMS EMS inbox retrieved.", page, limit, total));
    }

    // -------------------- Helpers --------------------

    private async Task<RemsFormBuildScreen> BuildScreenAsync(REMS rems, REMSForm? form, CancellationToken cancellationToken)
    {
        var names = await _users.GetFullNamesAsync(
            rems.CSEId is { } cse ? new[] { cse } : Array.Empty<Guid>(), cancellationToken);

        var cseRef = rems.CSEId is { } cseId
            ? new RemsUserRef(cseId, names.TryGetValue(cseId, out var name) ? name : string.Empty)
            : null;

        var formInfo = form is null
            ? null
            : new RemsFormInfo(
                form.Id,
                form.IndustryGroup,
                form.InviteCode,
                BuildFormLink(form.InviteCode),
                form.Status.ToString(),
                form.SentOnUtc,
                form.SubmittedOnUtc,
                form.InviteLockedOnUtc,
                form.SentOnUtc is not null);

        return new RemsFormBuildScreen(
            rems.Id, rems.REMSNumber, rems.Title, rems.RequestedClientName, rems.Status,
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
            $"{rems.REMSNumber} — {rems.Title}",
            EntityType.Rems,
            rems.Id);

    private static CreateNotificationDto FormSentNotification(Guid recipientId, REMS rems)
        => new(
            recipientId,
            NotificationType.RemsFormSent,
            "A REMS onboarding form was sent to the client",
            $"{rems.REMSNumber} — {rems.Title}",
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
