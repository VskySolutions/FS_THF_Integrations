namespace IntegrationHub.Application.Abstractions.Email;

/// <summary>
/// Low-level SMTP client: opens a connection to the configured host/port, applies the requested
/// transport security and authentication scheme, sends a single message, and closes the connection.
/// Used by <c>ISmtpAccountService</c> for test sends today and by any future notification service for
/// production sends. Enforces a 15-second timeout and surfaces a categorised
/// <see cref="SmtpSendResult"/> rather than throwing. Does not retry — the caller decides.
/// </summary>
public interface ISmtpEmailSender
{
    Task<SmtpSendResult> SendAsync(SmtpAccountCredentials credentials, SmtpMessage message, CancellationToken cancellationToken = default);
}
