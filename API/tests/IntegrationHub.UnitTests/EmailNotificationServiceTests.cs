using FluentAssertions;
using IntegrationHub.Application.Abstractions.Email;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Security;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Infrastructure.Email;
using IntegrationHub.Shared.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace IntegrationHub.UnitTests;

public class EmailNotificationServiceTests
{
    private readonly Mock<IEmailTemplateService> _templates = new();
    private readonly Mock<ISmtpAccountRepository> _smtpAccounts = new();
    private readonly Mock<ICredentialEncryptionService> _encryption = new();
    private readonly Mock<ISmtpEmailSender> _sender = new();
    private readonly Mock<ITenantRepository> _tenants = new();

    public EmailNotificationServiceTests()
    {
        _encryption.Setup(e => e.Decrypt(It.IsAny<string>())).Returns<string>(c => c.StartsWith("enc:") ? c[4..] : c);
        _templates.Setup(t => t.RenderEffectiveAsync(It.IsAny<Guid?>(), It.IsAny<EmailTemplateKey>(), It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RenderedEmail("Subject", "<p>Body</p>"));
    }

    private EmailNotificationService Create() => new(
        _templates.Object, _smtpAccounts.Object, _encryption.Object, _sender.Object, _tenants.Object,
        Options.Create(new AppOptions { BaseUrl = "https://app.example.com" }),
        NullLogger<EmailNotificationService>.Instance);

    private static SmtpAccount ActiveAccount(Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        AccountName = "Primary",
        Host = "smtp.example.com",
        Port = 587,
        EncryptionType = SmtpEncryptionType.StartTls,
        AuthType = SmtpAuthType.Login,
        Username = "user",
        EncryptedPassword = "enc:s3cret",
        FromName = "Acme",
        FromEmail = "noreply@acme.com",
        IsActive = true,
    };

    private static IReadOnlyDictionary<string, string?> Model() =>
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["FullName"] = "Jane" };

    [Fact]
    public async Task SendAsync_returns_false_when_recipient_is_empty()
    {
        (await Create().SendAsync(Guid.NewGuid(), EmailTemplateKey.Welcome, "", Model(), default)).Should().BeFalse();
        _sender.Verify(s => s.SendAsync(It.IsAny<SmtpAccountCredentials>(), It.IsAny<SmtpMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_returns_false_when_no_active_smtp_account()
    {
        var tenant = Guid.NewGuid();
        _smtpAccounts.Setup(a => a.GetActiveAsync(tenant, It.IsAny<CancellationToken>())).ReturnsAsync((SmtpAccount?)null);

        (await Create().SendAsync(tenant, EmailTemplateKey.Welcome, "to@x.com", Model(), default)).Should().BeFalse();
        _sender.Verify(s => s.SendAsync(It.IsAny<SmtpAccountCredentials>(), It.IsAny<SmtpMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_renders_and_sends_with_decrypted_credentials_and_augmented_model()
    {
        var tenant = Guid.NewGuid();
        _smtpAccounts.Setup(a => a.GetActiveAsync(tenant, It.IsAny<CancellationToken>())).ReturnsAsync(ActiveAccount(tenant));
        _tenants.Setup(t => t.GetByIdAsync(tenant, It.IsAny<CancellationToken>())).ReturnsAsync(new Tenant { Id = tenant, Name = "Acme Corp" });
        IReadOnlyDictionary<string, string?>? capturedModel = null;
        _templates.Setup(t => t.RenderEffectiveAsync(tenant, EmailTemplateKey.UserInvitation, It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid?, EmailTemplateKey, IReadOnlyDictionary<string, string?>, CancellationToken>((_, _, m, _) => capturedModel = m)
            .ReturnsAsync(new RenderedEmail("Welcome", "<p>Hi</p>"));
        SmtpAccountCredentials? creds = null;
        SmtpMessage? message = null;
        _sender.Setup(s => s.SendAsync(It.IsAny<SmtpAccountCredentials>(), It.IsAny<SmtpMessage>(), It.IsAny<CancellationToken>()))
            .Callback<SmtpAccountCredentials, SmtpMessage, CancellationToken>((c, m, _) => { creds = c; message = m; })
            .ReturnsAsync(SmtpSendResult.Ok("250 OK"));

        var result = await Create().SendAsync(tenant, EmailTemplateKey.UserInvitation, "to@x.com", Model(), default);

        result.Should().BeTrue();
        creds!.Password.Should().Be("s3cret");
        message!.IsHtml.Should().BeTrue();
        message.ToEmail.Should().Be("to@x.com");
        capturedModel!["TenantName"].Should().Be("Acme Corp");
        capturedModel["LoginUrl"].Should().Be("https://app.example.com");
        capturedModel["FullName"].Should().Be("Jane");
    }

    [Fact]
    public async Task SendAsync_returns_false_without_throwing_on_send_failure()
    {
        var tenant = Guid.NewGuid();
        _smtpAccounts.Setup(a => a.GetActiveAsync(tenant, It.IsAny<CancellationToken>())).ReturnsAsync(ActiveAccount(tenant));
        _sender.Setup(s => s.SendAsync(It.IsAny<SmtpAccountCredentials>(), It.IsAny<SmtpMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SmtpSendResult.Failure(SmtpErrorCategory.AuthenticationFailure, "535"));

        (await Create().SendAsync(tenant, EmailTemplateKey.Welcome, "to@x.com", Model(), default)).Should().BeFalse();
    }
}
