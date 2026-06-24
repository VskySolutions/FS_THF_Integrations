namespace IntegrationHub.Domain.Enums;

/// <summary>
/// Transport security applied when connecting to an SMTP server (SMTP Email Accounts).
/// </summary>
public enum SmtpEncryptionType
{
    /// <summary>No transport encryption (plaintext connection).</summary>
    None = 0,

    /// <summary>Opportunistic TLS: connect in plaintext, then upgrade via the STARTTLS command.</summary>
    StartTls = 1,

    /// <summary>Implicit TLS: the connection is encrypted from the start (SMTPS, typically port 465).</summary>
    SslTls = 2,

    /// <summary>Auto-negotiate transport security based on the server's capabilities and port.</summary>
    Auto = 3,
}
