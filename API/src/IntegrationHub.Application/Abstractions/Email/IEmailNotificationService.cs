using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Application.Abstractions.Email;

/// <summary>
/// Sends a transactional email for a tenant: resolves the effective template, renders it with the
/// supplied model (augmented with tenant name + app login URL), and dispatches it through the tenant's
/// active SMTP account. Best-effort — it never throws and returns false when no active SMTP account is
/// configured or the send fails, so callers (user creation, password reset, …) are never blocked.
/// </summary>
public interface IEmailNotificationService
{
    Task<bool> SendAsync(
        Guid tenantId,
        EmailTemplateKey key,
        string? toEmail,
        IReadOnlyDictionary<string, string?> model,
        CancellationToken cancellationToken = default);
}
