using System.Net.Sockets;
using IntegrationHub.Application.Abstractions.Email;
using IntegrationHub.Domain.Enums;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace IntegrationHub.Infrastructure.Email;

/// <summary>
/// MailKit-backed <see cref="ISmtpEmailSender"/>. Opens a connection, applies the requested transport
/// security and SASL authentication mechanism, sends one message, and disconnects. Enforces a
/// 15-second timeout (SMTP Email Accounts ADR-002) and maps exceptions to a categorised
/// <see cref="SmtpSendResult"/> rather than throwing. Does not retry.
/// </summary>
internal sealed class SmtpEmailSender : ISmtpEmailSender
{
    private static readonly TimeSpan DefaultSendTimeout = TimeSpan.FromSeconds(15);

    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly TimeSpan _sendTimeout;

    /// <param name="sendTimeout">Overall connect → auth → send budget. Defaults to 15 seconds (ADR-002); overridable for tests.</param>
    public SmtpEmailSender(ILogger<SmtpEmailSender> logger, TimeSpan? sendTimeout = null)
    {
        _logger = logger;
        _sendTimeout = sendTimeout ?? DefaultSendTimeout;
    }

    public async Task<SmtpSendResult> SendAsync(SmtpAccountCredentials credentials, SmtpMessage message, CancellationToken cancellationToken = default)
    {
        // A 15-second budget for the entire connect → auth → send sequence.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_sendTimeout);
        var token = timeoutCts.Token;

        using var client = new SmtpClient { Timeout = (int)_sendTimeout.TotalMilliseconds };

        try
        {
            var mime = BuildMessage(credentials, message);

            await client.ConnectAsync(credentials.Host, credentials.Port, ToSocketOptions(credentials.EncryptionType), token);

            // Authenticate unless explicitly anonymous or no username was supplied; the chosen scheme
            // forces a single SASL mechanism.
            if (credentials.AuthType != SmtpAuthType.None && !string.IsNullOrEmpty(credentials.Username))
            {
                client.AuthenticationMechanisms.Clear();
                client.AuthenticationMechanisms.Add(ToMechanism(credentials.AuthType));
                await client.AuthenticateAsync(credentials.Username, credentials.Password ?? string.Empty, token);
            }

            var serverResponse = await client.SendAsync(mime, token);
            await client.DisconnectAsync(quit: true, token);

            return SmtpSendResult.Ok(serverResponse);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The caller did not cancel — our own 15s budget elapsed.
            return SmtpSendResult.Failure(SmtpErrorCategory.Timeout, $"The send did not complete within {_sendTimeout.TotalSeconds:0} seconds.");
        }
        catch (AuthenticationException ex)
        {
            return SmtpSendResult.Failure(SmtpErrorCategory.AuthenticationFailure, ex.Message);
        }
        catch (SmtpCommandException ex) when (ex.ErrorCode is SmtpErrorCode.RecipientNotAccepted or SmtpErrorCode.SenderNotAccepted)
        {
            return SmtpSendResult.Failure(SmtpErrorCategory.InvalidRecipient, ex.Message);
        }
        catch (SslHandshakeException ex)
        {
            return SmtpSendResult.Failure(SmtpErrorCategory.TlsHandshakeFailure, ex.Message);
        }
        catch (SocketException ex)
        {
            return SmtpSendResult.Failure(SmtpErrorCategory.ConnectionRefused, ex.Message);
        }
        catch (TimeoutException ex)
        {
            return SmtpSendResult.Failure(SmtpErrorCategory.Timeout, ex.Message);
        }
        catch (Exception ex) when (ex is SmtpProtocolException or SmtpCommandException or IOException)
        {
            _logger.LogWarning(ex, "SMTP send to {Host}:{Port} failed.", credentials.Host, credentials.Port);
            return SmtpSendResult.Failure(SmtpErrorCategory.Unknown, ex.Message);
        }
    }

    private static MimeMessage BuildMessage(SmtpAccountCredentials credentials, SmtpMessage message)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(credentials.FromName, credentials.FromEmail));
        mime.To.Add(MailboxAddress.Parse(message.ToEmail));
        mime.Subject = message.Subject;
        mime.Body = new TextPart(message.IsHtml ? "html" : "plain") { Text = message.Body };
        return mime;
    }

    private static SecureSocketOptions ToSocketOptions(SmtpEncryptionType encryption) => encryption switch
    {
        SmtpEncryptionType.None => SecureSocketOptions.None,
        SmtpEncryptionType.StartTls => SecureSocketOptions.StartTls,
        SmtpEncryptionType.SslTls => SecureSocketOptions.SslOnConnect,
        SmtpEncryptionType.Auto => SecureSocketOptions.Auto,
        _ => SecureSocketOptions.Auto,
    };

    private static string ToMechanism(SmtpAuthType authType) => authType switch
    {
        SmtpAuthType.Plain => "PLAIN",
        SmtpAuthType.Login => "LOGIN",
        SmtpAuthType.CramMd5 => "CRAM-MD5",
        _ => "PLAIN",
    };
}
