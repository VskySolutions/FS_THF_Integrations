namespace IntegrationHub.Domain.Enums;

/// <summary>
/// Categorised failure reason for an SMTP send attempt, surfaced to administrators when a test
/// (or future production) send fails so they can act on the specific problem.
/// </summary>
public enum SmtpErrorCategory
{
    /// <summary>The server rejected the supplied credentials.</summary>
    AuthenticationFailure = 0,

    /// <summary>The connection to the host/port was refused or could not be established.</summary>
    ConnectionRefused = 1,

    /// <summary>The TLS/SSL handshake failed (certificate or protocol mismatch).</summary>
    TlsHandshakeFailure = 2,

    /// <summary>The server rejected the recipient address.</summary>
    InvalidRecipient = 3,

    /// <summary>The operation exceeded the configured timeout.</summary>
    Timeout = 4,

    /// <summary>An unspecified SMTP error not covered by the more specific categories.</summary>
    Unknown = 5,
}
