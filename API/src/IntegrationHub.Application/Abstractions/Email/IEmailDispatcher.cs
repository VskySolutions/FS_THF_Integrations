using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Application.Abstractions.Email;

/// <summary>
/// Enqueues transactional emails for background delivery so the request thread is never blocked on the
/// SMTP round-trip. The actual send is performed by a Hangfire job that calls
/// <see cref="IEmailNotificationService"/>. Abstracted so the Api/Application layers can queue an email
/// without referencing Hangfire directly (mirrors <c>ICustomerSyncDispatcher</c>).
/// </summary>
public interface IEmailDispatcher
{
    /// <summary>Queues a transactional email for best-effort delivery in the background (fire-and-forget).</summary>
    void Enqueue(Guid tenantId, EmailTemplateKey key, string? toEmail, IReadOnlyDictionary<string, string?> model);
}
