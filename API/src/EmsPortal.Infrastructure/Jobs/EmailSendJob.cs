using EmsPortal.Application.Abstractions.Email;
using EmsPortal.Domain.Enums;

namespace EmsPortal.Infrastructure.Jobs;

/// <summary>
/// Hangfire job that delivers a single transactional email via <see cref="IEmailNotificationService"/>.
/// Best-effort: the underlying send never throws, so a failed send or a tenant with no active SMTP
/// account simply completes the job quietly. Resolved from DI by the Hangfire server.
/// </summary>
public sealed class EmailSendJob
{
    private readonly IEmailNotificationService _email;

    public EmailSendJob(IEmailNotificationService email) => _email = email;

    /// <summary>
    /// Renders and sends the queued email. The model is a concrete dictionary for Hangfire serialization;
    /// <paramref name="messageId"/> pins the outbound Message-ID when supplied (WO-121), else null.
    /// </summary>
    public Task SendAsync(Guid tenantId, EmailTemplateKey key, string? toEmail, Dictionary<string, string?> model, string? messageId, CancellationToken cancellationToken)
        => _email.SendAsync(tenantId, key, toEmail, model, messageId, null, null, cancellationToken);

    /// <summary>
    /// As <see cref="SendAsync(Guid, EmailTemplateKey, string?, Dictionary{string, string?}, string?, CancellationToken)"/>,
    /// but with a subject / body an admin composed themselves, which replace the rendered template's.
    /// <para>
    /// A SEPARATE method rather than two more optional parameters: Hangfire serialises the method call it
    /// is given, so jobs already queued under the old signature must keep resolving after a deploy.
    /// </para>
    /// </summary>
    public Task SendComposedAsync(
        Guid tenantId, EmailTemplateKey key, string? toEmail, Dictionary<string, string?> model,
        string? messageId, string? subjectOverride, string? bodyOverride, CancellationToken cancellationToken)
        => _email.SendAsync(tenantId, key, toEmail, model, messageId, subjectOverride, bodyOverride, cancellationToken);
}
