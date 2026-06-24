namespace IntegrationHub.Domain.Enums;

/// <summary>
/// Authentication scheme used against an SMTP server (SMTP Email Accounts).
/// </summary>
public enum SmtpAuthType
{
    /// <summary>No authentication; the server accepts the message anonymously.</summary>
    None = 0,

    /// <summary>SASL PLAIN.</summary>
    Plain = 1,

    /// <summary>SASL LOGIN.</summary>
    Login = 2,

    /// <summary>SASL CRAM-MD5 (challenge-response; the password is never sent in the clear).</summary>
    CramMd5 = 3,
}
