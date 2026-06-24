using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Domain.Entities;

/// <summary>
/// A tenant's outgoing mail (SMTP) server account. Each tenant maintains a pool of accounts with
/// at most one marked <see cref="IsActive"/>; the active account is used for all platform
/// notifications. The password is stored encrypted (never in plaintext) and is never returned in
/// any API response. Inherits the standard audit/soft-delete fields from <see cref="AuditableEntity"/>.
/// </summary>
public class SmtpAccount : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant isolation; subject to the ambient EF query filter).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Friendly account name. Unique per tenant (amongst non-deleted rows).</summary>
    public string AccountName { get; set; } = string.Empty;

    /// <summary>SMTP server host name.</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>SMTP server port (1–65535).</summary>
    public int Port { get; set; }

    /// <summary>Transport security applied when connecting.</summary>
    public SmtpEncryptionType EncryptionType { get; set; } = SmtpEncryptionType.Auto;

    /// <summary>Authentication scheme used against the server.</summary>
    public SmtpAuthType AuthType { get; set; } = SmtpAuthType.Login;

    /// <summary>Login user name (optional; not required for <see cref="SmtpAuthType.None"/>).</summary>
    public string? Username { get; set; }

    /// <summary>The login password, encrypted via <c>ICredentialEncryptionService</c>. Never returned to callers.</summary>
    public string? EncryptedPassword { get; set; }

    /// <summary>Display name used in the From header of sent messages.</summary>
    public string FromName { get; set; } = string.Empty;

    /// <summary>From address used for sent messages.</summary>
    public string FromEmail { get; set; } = string.Empty;

    /// <summary>Whether this is the tenant's active sending account. At most one per tenant (DB-enforced).</summary>
    public bool IsActive { get; set; }
}
