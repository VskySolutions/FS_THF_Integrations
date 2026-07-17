using global::Hangfire;
using EmsPortal.Application.Abstractions.Email;
using EmsPortal.Domain.Enums;

namespace EmsPortal.Infrastructure.Jobs;

/// <summary>
/// Hangfire-backed <see cref="IEmailDispatcher"/>. Enqueues a one-off <see cref="EmailSendJob"/> so the
/// SMTP send happens on a worker rather than on the request thread.
/// </summary>
internal sealed class EmailDispatcher : IEmailDispatcher
{
    private readonly IBackgroundJobClient _backgroundJobs;

    public EmailDispatcher(IBackgroundJobClient backgroundJobs)
    {
        _backgroundJobs = backgroundJobs;
    }

    public void Enqueue(Guid tenantId, EmailTemplateKey key, string? toEmail, IReadOnlyDictionary<string, string?> model)
    {
        // Hangfire serializes the call arguments, so copy the model into a concrete dictionary.
        var payload = new Dictionary<string, string?>(model, StringComparer.OrdinalIgnoreCase);
        _backgroundJobs.Enqueue<EmailSendJob>(job => job.SendAsync(tenantId, key, toEmail, payload, CancellationToken.None));
    }
}
