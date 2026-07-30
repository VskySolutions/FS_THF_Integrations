using EmsPortal.Application.Abstractions.Email;
using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Application.Abstractions.Security;
using EmsPortal.Domain.Enums;
using EmsPortal.Shared.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmsPortal.Infrastructure.Email;

/// <summary>
/// Renders a tenant's effective template and dispatches it through the tenant's active SMTP account.
/// Best-effort: it never throws, logging and returning false when there is no active account or the
/// send fails, so the calling action (user creation, password reset, …) is never blocked.
/// </summary>
internal sealed class EmailNotificationService : IEmailNotificationService
{
    private readonly IEmailTemplateService _templates;
    private readonly ISmtpAccountRepository _smtpAccounts;
    private readonly ICredentialEncryptionService _encryption;
    private readonly ISmtpEmailSender _sender;
    private readonly ITenantRepository _tenants;
    private readonly AppOptions _appOptions;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(
        IEmailTemplateService templates,
        ISmtpAccountRepository smtpAccounts,
        ICredentialEncryptionService encryption,
        ISmtpEmailSender sender,
        ITenantRepository tenants,
        IOptions<AppOptions> appOptions,
        ILogger<EmailNotificationService> logger)
    {
        _templates = templates;
        _smtpAccounts = smtpAccounts;
        _encryption = encryption;
        _sender = sender;
        _tenants = tenants;
        _appOptions = appOptions.Value;
        _logger = logger;
    }

    public async Task<bool> HasActiveSenderAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await _smtpAccounts.GetActiveAsync(tenantId, cancellationToken) is not null;

    public async Task<bool> SendAsync(
        Guid tenantId,
        EmailTemplateKey key,
        string? toEmail,
        IReadOnlyDictionary<string, string?> model,
        CancellationToken cancellationToken = default)
    {
        // Central email allowlist (WO-124, AC-ETPL-005.5): only account-security + REMS external templates
        // may be emailed. Every send funnels through here, so no caller can bypass this gate.
        if (!EmailSendPolicy.IsEmailAllowed(key))
        {
            _logger.LogWarning("Blocked email for template {TemplateKey} (tenant {TenantId}): not on the email allowlist; this type is in-app only.", key, tenantId);
            return false;
        }

        if (string.IsNullOrWhiteSpace(toEmail))
        {
            return false;
        }

        try
        {
            var account = await _smtpAccounts.GetActiveAsync(tenantId, cancellationToken);
            if (account is null)
            {
                _logger.LogInformation("Skipping {TemplateKey} email for tenant {TenantId}: no active SMTP account.", key, tenantId);
                return false;
            }

            // Common values every template can use; caller-supplied values take precedence.
            var merged = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["LoginUrl"] = _appOptions.BaseUrl,
                ["AppBaseUrl"] = _appOptions.BaseUrl,
            };
            var tenant = await _tenants.GetByIdAsync(tenantId, cancellationToken);
            if (tenant is not null)
            {
                merged["TenantName"] = tenant.Name;
            }
            foreach (var kv in model)
            {
                merged[kv.Key] = kv.Value;
            }

            var rendered = await _templates.RenderEffectiveAsync(tenantId, key, merged, cancellationToken);
            if (rendered is null)
            {
                return false;
            }

            var credentials = new SmtpAccountCredentials(
                account.Host,
                account.Port,
                account.EncryptionType,
                account.AuthType,
                account.Username,
                string.IsNullOrEmpty(account.EncryptedPassword) ? null : _encryption.Decrypt(account.EncryptedPassword),
                account.FromName,
                account.FromEmail);

            var message = new SmtpMessage(toEmail.Trim(), rendered.Subject, rendered.Body, IsHtml: true);
            var result = await _sender.SendAsync(credentials, message, cancellationToken);
            if (!result.Success)
            {
                _logger.LogWarning("Failed to send {TemplateKey} email for tenant {TenantId}: {Category} {Error}",
                    key, tenantId, result.ErrorCategory, result.ErrorMessage);
            }
            return result.Success;
        }
        catch (Exception ex)
        {
            // Notifications must never break the primary action.
            _logger.LogWarning(ex, "Error sending {TemplateKey} email for tenant {TenantId}.", key, tenantId);
            return false;
        }
    }
}
