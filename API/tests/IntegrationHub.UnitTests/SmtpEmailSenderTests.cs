using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using IntegrationHub.Application.Abstractions.Email;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Infrastructure.Email;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntegrationHub.UnitTests;

public class SmtpEmailSenderTests
{
    private static SmtpAccountCredentials Creds(int port, SmtpAuthType auth = SmtpAuthType.None) =>
        new("127.0.0.1", port, SmtpEncryptionType.None, auth, "user", "pass", "Acme", "from@acme.com");

    private static SmtpMessage Message() => new("to@example.com", "Test subject", "Test body");

    [Fact]
    public async Task SendAsync_returns_ConnectionRefused_when_nothing_is_listening()
    {
        // Reserve a port then release it, guaranteeing the connection is refused.
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        var sender = new SmtpEmailSender(NullLogger<SmtpEmailSender>.Instance, TimeSpan.FromSeconds(5));
        var result = await sender.SendAsync(Creds(port), Message());

        result.Success.Should().BeFalse();
        result.ErrorCategory.Should().Be(SmtpErrorCategory.ConnectionRefused);
    }

    [Fact]
    public async Task SendAsync_returns_Timeout_when_the_server_never_responds()
    {
        // Server accepts the TCP connection but never sends the SMTP greeting.
        using var server = new FakeSmtpServer(greet: false);

        var sender = new SmtpEmailSender(NullLogger<SmtpEmailSender>.Instance, TimeSpan.FromMilliseconds(400));
        var result = await sender.SendAsync(Creds(server.Port), Message());

        result.Success.Should().BeFalse();
        result.ErrorCategory.Should().Be(SmtpErrorCategory.Timeout);
    }

    [Fact]
    public async Task SendAsync_returns_AuthenticationFailure_when_the_server_rejects_credentials()
    {
        using var server = new FakeSmtpServer(greet: true, authResponse: "535 5.7.8 Authentication failed");

        var sender = new SmtpEmailSender(NullLogger<SmtpEmailSender>.Instance, TimeSpan.FromSeconds(5));
        var result = await sender.SendAsync(Creds(server.Port, SmtpAuthType.Plain), Message());

        result.Success.Should().BeFalse();
        result.ErrorCategory.Should().Be(SmtpErrorCategory.AuthenticationFailure);
    }

    /// <summary>
    /// A minimal scripted SMTP server for exercising the sender's error categorisation over a real socket.
    /// </summary>
    private sealed class FakeSmtpServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();

        public int Port { get; }

        public FakeSmtpServer(bool greet, string? authResponse = null)
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _ = AcceptLoopAsync(greet, authResponse, _cts.Token);
        }

        private async Task AcceptLoopAsync(bool greet, string? authResponse, CancellationToken ct)
        {
            try
            {
                using var client = await _listener.AcceptTcpClientAsync(ct);
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII);
                await using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true, NewLine = "\r\n" };

                if (!greet)
                {
                    // Hold the connection open without greeting so the client times out.
                    await Task.Delay(Timeout.Infinite, ct);
                    return;
                }

                await writer.WriteLineAsync("220 localhost ESMTP Fake");
                string? line;
                while ((line = await reader.ReadLineAsync(ct)) != null)
                {
                    if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase) || line.StartsWith("HELO", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("250-localhost");
                        await writer.WriteLineAsync("250 AUTH PLAIN LOGIN");
                    }
                    else if (line.StartsWith("AUTH", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync(authResponse ?? "235 2.7.0 Authentication succeeded");
                    }
                    else if (line.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("221 Bye");
                        break;
                    }
                    else
                    {
                        await writer.WriteLineAsync("250 OK");
                    }
                }
            }
            catch
            {
                // Listener stopped, client disconnected, or cancellation — expected during teardown.
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Stop();
            _cts.Dispose();
        }
    }
}
