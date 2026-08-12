using EmsPortal.Domain.Enums;

namespace EmsPortal.Application.Abstractions.Email;

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
        string? messageId = null,
        // An already-composed subject / body that REPLACE the rendered template's — what an admin edited
        // in a send dialog before confirming. The template is still resolved (it is what they were shown,
        // and its absence is still a reason not to send); these just win over it. Null = render as normal.
        string? subjectOverride = null,
        string? bodyOverride = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fast check (no SMTP round-trip) of whether the tenant has an active SMTP account, i.e. whether a
    /// queued email will actually be attempted. Used by flows that report email status to the caller
    /// while the send itself runs in the background.
    /// </summary>
    Task<bool> HasActiveSenderAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
