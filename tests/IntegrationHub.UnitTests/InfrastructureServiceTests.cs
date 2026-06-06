using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using FluentAssertions;
using IntegrationHub.Application.Abstractions.Auditing;
using IntegrationHub.Application.Abstractions.Connectors;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Retry;
using IntegrationHub.Application.Abstractions.Security;
using IntegrationHub.Application.Abstractions.Tenancy;
using IntegrationHub.Application.Concur;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Infrastructure.Auditing;
using IntegrationHub.Infrastructure.Connectors;
using IntegrationHub.Infrastructure.Retry;
using IntegrationHub.Infrastructure.Security;
using IntegrationHub.Infrastructure.Tenancy;
using IntegrationHub.Shared.Configuration;
using IntegrationHub.Shared.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace IntegrationHub.UnitTests;

// WO-32: retry framework + audit service.
public class RetryQueueManagerTests
{
    private readonly Mock<IIntegrationJobRepository> _jobs = new();
    private readonly Mock<IRetryQueueRepository> _retryQueue = new();
    private readonly Mock<IDeadLetterQueueManager> _deadLetter = new();
    private readonly Mock<IIntegrationJobExecutor> _executor = new();
    private readonly Mock<IAuditTrailService> _audit = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private RetryQueueManager Create() => new(
        _jobs.Object, _retryQueue.Object, _deadLetter.Object, _executor.Object,
        _audit.Object, _unitOfWork.Object, Options.Create(new RetryOptions()), NullLogger<RetryQueueManager>.Instance);

    [Fact]
    public async Task Validation_failure_is_dead_lettered_immediately()
    {
        var job = TestData.Job();
        _jobs.Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);

        await Create().RegisterFailureAsync(job.Id, isRetriable: false, "bad data", default);

        _deadLetter.Verify(d => d.MoveToDeadLetterAsync(job, It.IsAny<RetryQueueEntry?>(), "bad data", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task First_transient_failure_creates_pending_retry_entry()
    {
        var job = TestData.Job();
        _jobs.Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);
        _retryQueue.Setup(r => r.GetByJobIdAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync((RetryQueueEntry?)null);

        await Create().RegisterFailureAsync(job.Id, isRetriable: true, "timeout", default);

        _retryQueue.Verify(r => r.AddAsync(It.Is<RetryQueueEntry>(e => e.RetryCount == 1 && e.Status == RetryStatus.Pending), It.IsAny<CancellationToken>()), Times.Once);
        job.Status.Should().Be(IntegrationJobStatus.Failed);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Exhausted_attempts_dead_letter()
    {
        var job = TestData.Job();
        var entry = new RetryQueueEntry { Id = Guid.NewGuid(), JobId = job.Id, RetryCount = 4, Status = RetryStatus.Pending };
        _jobs.Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);
        _retryQueue.Setup(r => r.GetByJobIdAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(entry);

        await Create().RegisterFailureAsync(job.Id, isRetriable: true, "timeout", default);

        _deadLetter.Verify(d => d.MoveToDeadLetterAsync(job, entry, "timeout", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Manual_retry_of_non_failed_job_returns_false()
    {
        var job = TestData.Job(status: IntegrationJobStatus.Completed);
        _jobs.Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);

        (await Create().ManualRetryAsync(job.Id, "admin", default)).Should().BeFalse();
    }

    [Fact]
    public async Task Manual_retry_of_failed_job_resets_and_enqueues()
    {
        var job = TestData.Job(status: IntegrationJobStatus.Failed);
        _jobs.Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);
        _retryQueue.Setup(r => r.GetByJobIdAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync((RetryQueueEntry?)null);

        var result = await Create().ManualRetryAsync(job.Id, "admin", default);

        result.Should().BeTrue();
        job.Status.Should().Be(IntegrationJobStatus.Created);
        job.AttemptCount.Should().Be(0);
        _executor.Verify(e => e.EnqueueForExecutionAsync(job.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class DeadLetterQueueManagerTests
{
    [Fact]
    public async Task Move_marks_permanently_failed_writes_audit_and_removes_entry()
    {
        var jobs = new Mock<IIntegrationJobRepository>();
        var retry = new Mock<IRetryQueueRepository>();
        var audit = new Mock<IAuditTrailService>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var manager = new DeadLetterQueueManager(jobs.Object, retry.Object, audit.Object, unitOfWork.Object);

        var job = TestData.Job(status: IntegrationJobStatus.Failed);
        var entry = new RetryQueueEntry { Id = Guid.NewGuid(), JobId = job.Id, RetryCount = 4 };

        await manager.MoveToDeadLetterAsync(job, entry, "exhausted", default);

        job.Status.Should().Be(IntegrationJobStatus.PermanentlyFailed);
        audit.Verify(a => a.AddAsync(nameof(IntegrationJob), job.Id.ToString(), "DeadLettered", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        retry.Verify(r => r.Remove(entry), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class AuditTrailServiceTests
{
    [Fact]
    public async Task Uses_actor_accessor_when_performer_not_supplied()
    {
        var repo = new Mock<IAuditTrailRepository>();
        var actor = new Mock<IActorAccessor>();
        actor.Setup(a => a.GetCurrentActor()).Returns("system");
        AuditTrailEntry? captured = null;
        repo.Setup(r => r.AddAsync(It.IsAny<AuditTrailEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditTrailEntry, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        await new AuditTrailService(repo.Object, actor.Object).AddAsync("Job", "id-1", "Created");

        captured.Should().NotBeNull();
        captured!.PerformedBy.Should().Be("system");
        captured.Action.Should().Be("Created");
        captured.CreatedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }
}

// WO-43: JWT issuance + token version validation.
public class JwtTokenServiceTests
{
    [Fact]
    public void Issued_token_carries_expected_claims_and_super_admin_role()
    {
        var keyProvider = new RsaSigningKeyProvider(Options.Create(new AuthenticationOptions()));
        var service = new JwtTokenService(keyProvider, Options.Create(new AuthenticationOptions { AccessTokenMinutes = 60 }));

        var user = TestData.User(email: "a@b.com", tokenVersion: 3);
        var tenantId = Guid.NewGuid();
        user.TenantRoles.Add(TestData.Assignment(user.Id, tenantId, UserRole.SuperAdmin));

        var token = service.CreateAccessToken(user, tenantId);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.Token);

        jwt.Claims.Should().Contain(c => c.Type == ClaimTypeNames.Subject && c.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == "email" && c.Value == "a@b.com");
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypeNames.Role && c.Value == Roles.SuperAdmin);
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypeNames.TokenVersion && c.Value == "3");
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypeNames.ActiveTenantId && c.Value == tenantId.ToString());
        token.ExpiresInSeconds.Should().BeGreaterThan(0);
    }
}

public class DbTokenVersionValidatorTests
{
    private static DbTokenVersionValidator Create(User? user)
    {
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return new DbTokenVersionValidator(users.Object);
    }

    [Fact]
    public async Task Unknown_user_is_invalid()
        => (await Create(null).IsValidAsync(Guid.NewGuid(), 1)).Should().BeFalse();

    [Fact]
    public async Task Inactive_user_is_invalid()
        => (await Create(TestData.User(isActive: false)).IsValidAsync(Guid.NewGuid(), 1)).Should().BeFalse();

    [Fact]
    public async Task Lower_token_version_is_invalid()
        => (await Create(TestData.User(tokenVersion: 5)).IsValidAsync(Guid.NewGuid(), 4)).Should().BeFalse();

    [Fact]
    public async Task Equal_or_higher_version_on_active_user_is_valid()
        => (await Create(TestData.User(tokenVersion: 5)).IsValidAsync(Guid.NewGuid(), 5)).Should().BeTrue();
}

// WO-42: tenant API configuration service (decrypt + scope cache).
public class TenantApiConfigurationServiceTests
{
    [Fact]
    public async Task Returns_null_when_no_tenant_resolved()
    {
        var ctx = new Mock<ITenantContext>();
        ctx.SetupGet(c => c.IsResolved).Returns(false);
        var service = new TenantApiConfigurationService(ctx.Object, Mock.Of<ITenantApiConfigurationRepository>(), Mock.Of<ICredentialEncryptionService>());

        (await service.GetConcurConfigAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Decrypts_and_caches_within_scope()
    {
        var tenantId = Guid.NewGuid();
        var ctx = new Mock<ITenantContext>();
        ctx.SetupGet(c => c.IsResolved).Returns(true);
        ctx.SetupGet(c => c.TenantId).Returns(tenantId);

        var dto = new ConcurConfigDto("cid", "secret", "https://concur/", "uuid");
        var repo = new Mock<ITenantApiConfigurationRepository>();
        repo.Setup(r => r.GetAsync(tenantId, SystemName.Concur, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantApiConfiguration { Id = Guid.NewGuid(), TenantId = tenantId, System = SystemName.Concur, EncryptedCredentials = "cipher" });
        var enc = new Mock<ICredentialEncryptionService>();
        enc.Setup(e => e.Decrypt("cipher")).Returns(JsonSerializer.Serialize(dto));

        var service = new TenantApiConfigurationService(ctx.Object, repo.Object, enc.Object);

        var first = await service.GetConcurConfigAsync();
        var second = await service.GetConcurConfigAsync();

        first.Should().BeEquivalentTo(dto);
        second.Should().BeEquivalentTo(dto);
        repo.Verify(r => r.GetAsync(tenantId, SystemName.Concur, It.IsAny<CancellationToken>()), Times.Once); // cached
    }
}

// WO-34: transformer applies mappings with source fallback.
public class ConcurExpenseTransformerTests
{
    [Fact]
    public async Task Falls_back_to_source_values_when_no_mappings()
    {
        var mappings = new Mock<IMappingConfigurationRepository>();
        mappings.Setup(m => m.GetActiveByPairAsync(SystemName.Concur, SystemName.Maconomy, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MappingConfiguration>());

        var transformer = new ConcurExpenseTransformer(mappings.Object, new TransformationRuleEvaluator());
        var source = new IntegrationHub.Application.Abstractions.Connectors.Concur.ConcurExpenseReport(
            "R1", "E1", "Approved", DateTime.UtcNow, 100m, "USD",
            new[] { new IntegrationHub.Application.Abstractions.Connectors.Concur.ConcurExpenseLine("L1", "Meals", 50m, null, "lunch") });

        var result = await transformer.TransformAsync(source);

        result.Success.Should().BeTrue();
        result.Payload!.ReportId.Should().Be("R1");
        result.Payload.TotalAmount.Should().Be(100m);
        result.Payload.Lines.Should().HaveCount(1);
    }
}
