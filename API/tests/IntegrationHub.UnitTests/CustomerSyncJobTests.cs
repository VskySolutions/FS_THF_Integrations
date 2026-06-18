using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using IntegrationHub.Application.Abstractions.Connectors.Maconomy;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Tenancy;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Infrastructure.Jobs;
using IntegrationHub.Shared.Connectors;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace IntegrationHub.UnitTests;

// WO-66: on-demand Maconomy customer sync job.
public class CustomerSyncJobTests
{
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<ITenantRepository> _tenants = new();
    private readonly Mock<ICustomerRequestRepository> _requests = new();
    private readonly Mock<ICustomerAuditRepository> _audit = new();
    private readonly Mock<ITenantApiConfigurationService> _config = new();
    private readonly Mock<IMaconomyConnector> _maconomy = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IBackgroundJobClient> _backgroundJobs = new();

    private CustomerSyncJob Create() => new(
        _tenantContext.Object, _tenants.Object, _requests.Object, _audit.Object,
        _config.Object, _maconomy.Object, _unitOfWork.Object, _backgroundJobs.Object,
        NullLogger<CustomerSyncJob>.Instance);

    private (Guid TenantId, CustomerRequest Request) Arrange(CustomerRequestStatus status = CustomerRequestStatus.SyncInProgress, int syncAttempts = 0)
    {
        var tenant = TestData.Tenant();
        var request = new CustomerRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Status = status,
            CompanyName = "Acme",
            LegalName = "Acme Inc",
            EmailAddress = "a@acme.com",
            Country = "US",
            AddressLine1 = "1 St",
            SyncAttempts = syncAttempts,
        };
        _tenants.Setup(t => t.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        _requests.Setup(r => r.GetByIdForTenantAsync(request.Id, tenant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        return (tenant.Id, request);
    }

    [Fact]
    public async Task Missing_credentials_marks_failed_audits_and_does_not_call_connector()
    {
        var (tenantId, request) = Arrange();
        _config.Setup(c => c.GetMaconomyConfigAsync(It.IsAny<CancellationToken>())).ReturnsAsync((MaconomyConfigDto?)null);

        await Create().RunAsync(request.Id, tenantId, default);

        request.Status.Should().Be(CustomerRequestStatus.Failed);
        request.LastSyncError.Should().Contain("credentials");
        _tenantContext.Verify(c => c.Set(tenantId, It.IsAny<string>()), Times.Once);
        _audit.Verify(a => a.AddAsync(It.Is<CustomerAuditEntry>(e => e.ActionType == CustomerAuditActionType.SyncFailed), It.IsAny<CancellationToken>()), Times.Once);
        _maconomy.Verify(m => m.CreateCustomerAsync(It.IsAny<MaconomyCustomer>(), It.IsAny<CancellationToken>()), Times.Never);
        _backgroundJobs.Verify(b => b.Create(It.IsAny<Job>(), It.IsAny<ScheduledState>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Successful_sync_stores_customer_number_and_marks_synced()
    {
        var (tenantId, request) = Arrange();
        _config.Setup(c => c.GetMaconomyConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MaconomyConfigDto("https://maconomy/", "user", "pass"));
        _maconomy.Setup(m => m.CreateCustomerAsync(It.IsAny<MaconomyCustomer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConnectorResult<MaconomyWriteResult>.Ok(new MaconomyWriteResult("CUST-12345", Duplicate: false)));

        await Create().RunAsync(request.Id, tenantId, default);

        request.Status.Should().Be(CustomerRequestStatus.Synced);
        request.MaconomyCustomerNumber.Should().Be("CUST-12345");
        request.LastSyncError.Should().BeNull();
        request.SyncAttempts.Should().Be(1);
        _requests.Verify(r => r.Update(request), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Transient_failure_with_attempts_remaining_stays_in_progress_and_reschedules()
    {
        var (tenantId, request) = Arrange(syncAttempts: 0);
        _config.Setup(c => c.GetMaconomyConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MaconomyConfigDto("https://maconomy/", "user", "pass"));
        _maconomy.Setup(m => m.CreateCustomerAsync(It.IsAny<MaconomyCustomer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConnectorResult<MaconomyWriteResult>.Fail("timeout", isRetriable: true));

        await Create().RunAsync(request.Id, tenantId, default);

        // Status is left unchanged (still SyncInProgress); a retry is scheduled.
        request.Status.Should().Be(CustomerRequestStatus.SyncInProgress);
        request.LastSyncError.Should().Be("timeout");
        _backgroundJobs.Verify(b => b.Create(It.IsAny<Job>(), It.IsAny<ScheduledState>()), Times.Once);
    }

    [Fact]
    public async Task Non_retriable_failure_marks_failed_with_error()
    {
        var (tenantId, request) = Arrange();
        _config.Setup(c => c.GetMaconomyConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MaconomyConfigDto("https://maconomy/", "user", "pass"));
        _maconomy.Setup(m => m.CreateCustomerAsync(It.IsAny<MaconomyCustomer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConnectorResult<MaconomyWriteResult>.Fail("validation error", isRetriable: false));

        await Create().RunAsync(request.Id, tenantId, default);

        request.Status.Should().Be(CustomerRequestStatus.Failed);
        request.LastSyncError.Should().Be("validation error");
        _backgroundJobs.Verify(b => b.Create(It.IsAny<Job>(), It.IsAny<ScheduledState>()), Times.Never);
    }

    [Fact]
    public async Task Retriable_failure_with_attempts_exhausted_marks_failed()
    {
        // BackoffMinutes has 4 entries; SyncAttempts becomes 4 after the increment, so no retry remains.
        var (tenantId, request) = Arrange(syncAttempts: 3);
        _config.Setup(c => c.GetMaconomyConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MaconomyConfigDto("https://maconomy/", "user", "pass"));
        _maconomy.Setup(m => m.CreateCustomerAsync(It.IsAny<MaconomyCustomer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConnectorResult<MaconomyWriteResult>.Fail("still timing out", isRetriable: true));

        await Create().RunAsync(request.Id, tenantId, default);

        request.Status.Should().Be(CustomerRequestStatus.Failed);
        request.LastSyncError.Should().Be("still timing out");
        _backgroundJobs.Verify(b => b.Create(It.IsAny<Job>(), It.IsAny<ScheduledState>()), Times.Never);
    }
}
