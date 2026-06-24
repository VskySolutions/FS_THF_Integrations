using FluentAssertions;
using IntegrationHub.Application.Abstractions.Auditing;
using IntegrationHub.Application.Abstractions.Email;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Security;
using IntegrationHub.Application.Email;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using Moq;

namespace IntegrationHub.UnitTests;

public class SmtpAccountServiceTests
{
    private readonly Mock<ISmtpAccountRepository> _accounts = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICredentialEncryptionService> _encryption = new();
    private readonly Mock<IAuditTrailService> _audit = new();
    private readonly Mock<ISmtpEmailSender> _sender = new();

    public SmtpAccountServiceTests()
    {
        // Deterministic, reversible "encryption" so tests can assert round-trips.
        _encryption.Setup(e => e.Encrypt(It.IsAny<string>())).Returns<string>(p => $"enc:{p}");
        _encryption.Setup(e => e.Decrypt(It.IsAny<string>())).Returns<string>(c => c.StartsWith("enc:") ? c[4..] : c);
        // Run the transactional body inline so ActivateAsync executes as in production.
        _unitOfWork
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>((op, ct) => op(ct));
    }

    private SmtpAccountService Create()
        => new(_accounts.Object, _unitOfWork.Object, _encryption.Object, _audit.Object, _sender.Object);

    private static CreateSmtpAccountInput NewInput(Guid tenantId, string? password = "s3cret") => new(
        tenantId, "Primary", "smtp.example.com", 587,
        SmtpEncryptionType.StartTls, SmtpAuthType.Plain, "user", password, "Acme", "noreply@acme.com");

    private static SmtpAccount Existing(Guid tenantId, bool isActive = false, string? encryptedPassword = "enc:old") => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        AccountName = "Primary",
        Host = "smtp.example.com",
        Port = 587,
        EncryptionType = SmtpEncryptionType.StartTls,
        AuthType = SmtpAuthType.Plain,
        Username = "user",
        EncryptedPassword = encryptedPassword,
        FromName = "Acme",
        FromEmail = "noreply@acme.com",
        IsActive = isActive,
    };

    // ---- CreateAsync ----

    [Fact]
    public async Task CreateAsync_encrypts_password_before_persisting()
    {
        var tenant = Guid.NewGuid();
        _accounts.Setup(a => a.NameExistsAsync(tenant, "Primary", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _accounts.Setup(a => a.CountByTenantAsync(tenant, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await Create().CreateAsync(NewInput(tenant), default);

        result.EncryptedPassword.Should().Be("enc:s3cret");
        _encryption.Verify(e => e.Encrypt("s3cret"), Times.Once);
        _accounts.Verify(a => a.AddAsync(It.Is<SmtpAccount>(s => s.EncryptedPassword == "enc:s3cret"), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_auto_activates_the_first_account_for_a_tenant()
    {
        var tenant = Guid.NewGuid();
        _accounts.Setup(a => a.NameExistsAsync(tenant, "Primary", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _accounts.Setup(a => a.CountByTenantAsync(tenant, It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var result = await Create().CreateAsync(NewInput(tenant), default);

        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_does_not_auto_activate_when_accounts_already_exist()
    {
        var tenant = Guid.NewGuid();
        _accounts.Setup(a => a.NameExistsAsync(tenant, "Primary", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _accounts.Setup(a => a.CountByTenantAsync(tenant, It.IsAny<CancellationToken>())).ReturnsAsync(2);

        var result = await Create().CreateAsync(NewInput(tenant), default);

        result.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_throws_on_duplicate_name_and_does_not_persist()
    {
        var tenant = Guid.NewGuid();
        _accounts.Setup(a => a.NameExistsAsync(tenant, "Primary", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => Create().CreateAsync(NewInput(tenant), default);

        (await act.Should().ThrowAsync<SmtpAccountException>()).Which.Code.Should().Be(SmtpAccountErrorCodes.DuplicateName);
        _accounts.Verify(a => a.AddAsync(It.IsAny<SmtpAccount>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- UpdateAsync ----

    [Fact]
    public async Task UpdateAsync_preserves_existing_password_when_none_supplied()
    {
        var tenant = Guid.NewGuid();
        var existing = Existing(tenant);
        _accounts.Setup(a => a.GetByIdAsync(existing.Id, tenant, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _accounts.Setup(a => a.NameExistsAsync(tenant, "Primary", existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var input = new UpdateSmtpAccountInput("Primary", "smtp.example.com", 2525,
            SmtpEncryptionType.SslTls, SmtpAuthType.Login, "user", null, "Acme", "noreply@acme.com");
        var result = await Create().UpdateAsync(existing.Id, tenant, input, default);

        result!.EncryptedPassword.Should().Be("enc:old");
        result.Port.Should().Be(2525);
        _encryption.Verify(e => e.Encrypt(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_reencrypts_when_a_new_password_is_supplied()
    {
        var tenant = Guid.NewGuid();
        var existing = Existing(tenant);
        _accounts.Setup(a => a.GetByIdAsync(existing.Id, tenant, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _accounts.Setup(a => a.NameExistsAsync(tenant, "Primary", existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var input = new UpdateSmtpAccountInput("Primary", "smtp.example.com", 587,
            SmtpEncryptionType.StartTls, SmtpAuthType.Plain, "user", "newpass", "Acme", "noreply@acme.com");
        var result = await Create().UpdateAsync(existing.Id, tenant, input, default);

        result!.EncryptedPassword.Should().Be("enc:newpass");
        _encryption.Verify(e => e.Encrypt("newpass"), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_returns_null_when_not_found()
    {
        var tenant = Guid.NewGuid();
        var id = Guid.NewGuid();
        _accounts.Setup(a => a.GetByIdAsync(id, tenant, It.IsAny<CancellationToken>())).ReturnsAsync((SmtpAccount?)null);

        var input = new UpdateSmtpAccountInput("Primary", "h", 1, SmtpEncryptionType.None, SmtpAuthType.None, null, null, "n", "e@e.com");
        (await Create().UpdateAsync(id, tenant, input, default)).Should().BeNull();
    }

    // ---- ActivateAsync ----

    [Fact]
    public async Task ActivateAsync_atomically_swaps_active_and_writes_audit()
    {
        var tenant = Guid.NewGuid();
        var current = Existing(tenant, isActive: true);
        var target = Existing(tenant, isActive: false);
        _accounts.Setup(a => a.GetByIdAsync(target.Id, tenant, It.IsAny<CancellationToken>())).ReturnsAsync(target);
        _accounts.Setup(a => a.GetActiveAsync(tenant, It.IsAny<CancellationToken>())).ReturnsAsync(current);

        var result = await Create().ActivateAsync(target.Id, tenant, default);

        current.IsActive.Should().BeFalse();
        target.IsActive.Should().BeTrue();
        result!.ActivatedId.Should().Be(target.Id);
        result.DeactivatedId.Should().Be(current.Id);
        _unitOfWork.Verify(u => u.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Once);
        _audit.Verify(a => a.AddAsync("SmtpAccount", target.Id.ToString(), "SmtpAccountActivated", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActivateAsync_is_a_noop_and_writes_no_audit_when_already_active()
    {
        var tenant = Guid.NewGuid();
        var target = Existing(tenant, isActive: true);
        _accounts.Setup(a => a.GetByIdAsync(target.Id, tenant, It.IsAny<CancellationToken>())).ReturnsAsync(target);

        var result = await Create().ActivateAsync(target.Id, tenant, default);

        result!.ActivatedId.Should().Be(target.Id);
        result.DeactivatedId.Should().BeNull();
        _unitOfWork.Verify(u => u.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Never);
        _audit.Verify(a => a.AddAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- DeleteAsync ----

    [Fact]
    public async Task DeleteAsync_blocks_deletion_of_the_active_account()
    {
        var tenant = Guid.NewGuid();
        var active = Existing(tenant, isActive: true);
        _accounts.Setup(a => a.GetByIdAsync(active.Id, tenant, It.IsAny<CancellationToken>())).ReturnsAsync(active);

        var act = () => Create().DeleteAsync(active.Id, tenant, default);

        (await act.Should().ThrowAsync<SmtpAccountException>()).Which.Code.Should().Be(SmtpAccountErrorCodes.ActiveAccountDelete);
        _accounts.Verify(a => a.Remove(It.IsAny<SmtpAccount>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_soft_deletes_an_inactive_account()
    {
        var tenant = Guid.NewGuid();
        var inactive = Existing(tenant, isActive: false);
        _accounts.Setup(a => a.GetByIdAsync(inactive.Id, tenant, It.IsAny<CancellationToken>())).ReturnsAsync(inactive);

        var result = await Create().DeleteAsync(inactive.Id, tenant, default);

        result.Should().BeTrue();
        _accounts.Verify(a => a.Remove(inactive), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- TestSendAsync ----

    [Fact]
    public async Task TestSendAsync_decrypts_credentials_and_returns_success_without_audit()
    {
        var tenant = Guid.NewGuid();
        var account = Existing(tenant, encryptedPassword: "enc:s3cret");
        _accounts.Setup(a => a.GetByIdAsync(account.Id, tenant, It.IsAny<CancellationToken>())).ReturnsAsync(account);
        SmtpAccountCredentials? captured = null;
        _sender.Setup(s => s.SendAsync(It.IsAny<SmtpAccountCredentials>(), It.IsAny<SmtpMessage>(), It.IsAny<CancellationToken>()))
            .Callback<SmtpAccountCredentials, SmtpMessage, CancellationToken>((c, _, _) => captured = c)
            .ReturnsAsync(SmtpSendResult.Ok("250 OK"));

        var result = await Create().TestSendAsync(account.Id, tenant, "to@example.com", default);

        result!.Success.Should().BeTrue();
        result.SentAtUtc.Should().NotBeNull();
        result.ServerResponse.Should().Be("250 OK");
        captured!.Password.Should().Be("s3cret");
        _audit.Verify(a => a.AddAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TestSendAsync_returns_failure_result_not_exception_on_send_error()
    {
        var tenant = Guid.NewGuid();
        var account = Existing(tenant);
        _accounts.Setup(a => a.GetByIdAsync(account.Id, tenant, It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _sender.Setup(s => s.SendAsync(It.IsAny<SmtpAccountCredentials>(), It.IsAny<SmtpMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SmtpSendResult.Failure(SmtpErrorCategory.AuthenticationFailure, "535 auth failed"));

        var result = await Create().TestSendAsync(account.Id, tenant, "to@example.com", default);

        result!.Success.Should().BeFalse();
        result.ErrorCategory.Should().Be(SmtpErrorCategory.AuthenticationFailure);
        result.ErrorDetail.Should().Be("535 auth failed");
    }

    [Fact]
    public async Task TestSendAsync_returns_null_when_account_not_found()
    {
        var tenant = Guid.NewGuid();
        var id = Guid.NewGuid();
        _accounts.Setup(a => a.GetByIdAsync(id, tenant, It.IsAny<CancellationToken>())).ReturnsAsync((SmtpAccount?)null);

        (await Create().TestSendAsync(id, tenant, "to@example.com", default)).Should().BeNull();
        _sender.Verify(s => s.SendAsync(It.IsAny<SmtpAccountCredentials>(), It.IsAny<SmtpMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
