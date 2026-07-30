using global::Hangfire;
using EmsPortal.Application.Abstractions.Email;
using EmsPortal.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace EmsPortal.Infrastructure.Jobs;

/// <summary>
/// Hangfire-backed <see cref="IEmailDispatcher"/>. Enqueues a one-off <see cref="EmailSendJob"/> so the
/// SMTP send happens on a worker rather than on the request thread.
/// </summary>
internal sealed class EmailDispatcher : IEmailDispatcher
{
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ILogger<EmailDispatcher> _logger;

    public EmailDispatcher(IBackgroundJobClient backgroundJobs, ILogger<EmailDispatcher> logger)
    {
        _backgroundJobs = backgroundJobs;
        _logger = logger;
    }

    public void Enqueue(Guid tenantId, EmailTemplateKey key, string? toEmail, IReadOnlyDictionary<string, string?> model, string? messageId = null)
    {
        // Central email allowlist (WO-124, AC-ETPL-005.5): never queue a job for an in-app-only type.
        if (!EmailSendPolicy.IsEmailAllowed(key))
        {
            _logger.LogWarning("Dropped email for template {TemplateKey} (tenant {TenantId}): not on the email allowlist; this type is in-app only.", key, tenantId);
            return;
        }

        // Hangfire serializes the call arguments, so copy the model into a concrete dictionary.
        var payload = new Dictionary<string, string?>(model, StringComparer.OrdinalIgnoreCase);
        _backgroundJobs.Enqueue<EmailSendJob>(job => job.SendAsync(tenantId, key, toEmail, payload, messageId, CancellationToken.None));
    }
}
