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
        => _email.SendAsync(tenantId, key, toEmail, model, messageId, cancellationToken);
}
