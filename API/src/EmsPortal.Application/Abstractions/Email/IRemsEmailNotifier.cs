using EmsPortal.Domain.Enums;

namespace EmsPortal.Application.Abstractions.Email;

/// <summary>
/// Background dispatch of the two REMS external emails (WO-124). A thin, typed wrapper over
/// <see cref="IEmailDispatcher"/> that pins the <see cref="EmailTemplateKey"/> and placeholder token
/// names, so REMS flows (WO-112 / WO-113) can send a well-formed email without knowing template internals.
/// <para>
/// Delivery is best-effort and runs on a Hangfire worker via the shared pipeline: the effective template
/// is resolved (tenant override → platform default); if the tenant has no active SMTP account the send is
/// skipped with a warning; SMTP failures are logged and swallowed (AC-ETPL-005.1–005.4). Enqueuing never
/// throws and never blocks the caller — do not roll a REMS operation back on an email failure.
/// </para>
/// </summary>
public interface IRemsEmailNotifier
{
    /// <summary>
    /// Queues the "complete your form" email to a client for a REMS request. Pass <paramref name="messageId"/>
    /// (WO-121) to pin the outbound Message-ID so a delivery provider's callbacks can be correlated back to
    /// the request; it is the same value stored as <c>ProviderMessageId</c> on the Sent email event.
    /// </summary>
    void SendFormLink(Guid tenantId, string toEmail, RemsFormLinkEmail model, string? messageId = null);

    /// <summary>
    /// As <see cref="SendFormLink"/>, but delivering a subject / body the sending admin composed in the
    /// send dialog rather than the template's own. Either may be null to keep the template's version.
    /// The placeholder model is still supplied: it is what renders whatever was not overridden.
    /// </summary>
    void SendComposedFormLink(
        Guid tenantId, string toEmail, RemsFormLinkEmail model, string? subject, string? body, string? messageId = null);

    /// <summary>
    /// Queues a reminder to a client who has their form link but has not submitted yet. Same placeholders
    /// as the original send — it is the same request and the same link — but its own template, so the
    /// nudge reads as one. <paramref name="subject"/> / <paramref name="body"/> carry what the admin
    /// composed in the send dialog; either null keeps the template's version of that part.
    /// </summary>
    void SendComposedFormReminder(
        Guid tenantId, string toEmail, RemsFormLinkEmail model, string? subject, string? body, string? messageId = null);

    /// <summary>Queues the "form submitted" email to the assigned Admin + CSE for a REMS request.</summary>
    void SendFormSubmitted(Guid tenantId, string toEmail, RemsFormSubmittedEmail model);
}

/// <summary>
/// Placeholder values for <see cref="EmailTemplateKey.RemsFormLink"/>. <paramref name="FormLink"/> is the
/// full EMS form URL the caller builds from <c>App:BaseUrl</c> (AC-ETPL-006.3).
/// </summary>
public sealed record RemsFormLinkEmail(string ClientName, string FormLink, string RemsNumber);

/// <summary>
/// Placeholder values for <see cref="EmailTemplateKey.RemsFormSubmitted"/>. <paramref name="RequestLink"/>
/// is the full request URL the caller builds from <c>App:BaseUrl</c> (AC-ETPL-006.3).
/// </summary>
public sealed record RemsFormSubmittedEmail(string ClientName, string RemsNumber, string RequestLink, string SubmittedOn);
