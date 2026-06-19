using FluentAssertions;
using IntegrationHub.Api.Dashboard;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;

namespace IntegrationHub.UnitTests;

// WO-77: DashboardQueryService aggregation (KPIs, trends, windows, SLA, cross-tenant fan-out).
// Backed by an EF Core InMemory DbContext; external repos/health are mocked.
public class DashboardQueryServiceTests
{
    private readonly Mock<IRetryQueueRepository> _retries = new();
    private readonly Mock<ITenantRepository> _tenants = new();
    private readonly Mock<ITenantApiConfigurationRepository> _tenantConfigs = new();
    private readonly Mock<IJobScheduleConfigurationRepository> _schedules = new();
    private readonly HealthCheckService _health = new NoopHealthCheckService();

    public DashboardQueryServiceTests()
    {
        _retries.Setup(r => r.QueryAsync(It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<RetryQueueEntry>(), 0));
        _tenantConfigs.Setup(c => c.ListByTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TenantApiConfiguration>());
        _schedules.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<JobScheduleConfiguration>());
    }

    private DashboardQueryService Create(IntegrationHubDbContext db)
        => new(db, _retries.Object, _tenants.Object, _tenantConfigs.Object, _schedules.Object, _health);

    private static IntegrationJob Job(Guid tenantId, IntegrationJobStatus status, DateTime createdOnUtc, string interfaceName = "ExpenseImport")
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InterfaceName = interfaceName,
            Direction = IntegrationDirection.Inbound,
            SourceSystem = SystemName.Concur,
            TargetSystem = SystemName.Maconomy,
            Status = status,
            CreatedOnUtc = createdOnUtc,
            UpdatedOnUtc = createdOnUtc,
        };

    private static CustomerRequest Customer(Guid tenantId, CustomerRequestStatus status, DateTime createdOnUtc, DateTime updatedOnUtc)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Status = status,
            CompanyName = "Acme",
            LegalName = "Acme Inc",
            EmailAddress = "a@acme.com",
            Address = new Address { CountryName = "US", AddressLine1 = "1 St" },
            CreatedOnUtc = createdOnUtc,
            UpdatedOnUtc = updatedOnUtc,
        };

    // ---- Job KPI computation ----

    [Fact]
    public async Task GetJobs_computes_total_completed_failed_pending_and_success_rate()
    {
        var tenant = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using var db = DashboardTestDbContext.Create();
        await DashboardTestDbContext.SeedAsync(db,
            Job(tenant, IntegrationJobStatus.Completed, now.AddDays(-1)),
            Job(tenant, IntegrationJobStatus.Completed, now.AddDays(-1)),
            Job(tenant, IntegrationJobStatus.Completed, now.AddDays(-1)),
            Job(tenant, IntegrationJobStatus.Failed, now.AddDays(-1)),
            Job(tenant, IntegrationJobStatus.PermanentlyFailed, now.AddDays(-1)),
            Job(tenant, IntegrationJobStatus.Created, now.AddDays(-1)),
            Job(tenant, IntegrationJobStatus.Running, now.AddDays(-1)));

        var dto = await Create(db).GetJobsAsync(tenant, "7d", default);

        dto.Kpis.Total.Should().Be(7);
        dto.Kpis.Completed.Should().Be(3);
        dto.Kpis.Failed.Should().Be(2, "Failed + PermanentlyFailed");
        dto.Kpis.Pending.Should().Be(2, "Created + Running");
        // successRate = completed / (completed + failed) * 100 = 3 / 5 * 100 = 60.0
        dto.SuccessRate.Should().Be(60.0);
    }

    [Fact]
    public async Task GetJobs_success_rate_is_zero_when_no_completed_or_failed()
    {
        var tenant = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using var db = DashboardTestDbContext.Create();
        await DashboardTestDbContext.SeedAsync(db, Job(tenant, IntegrationJobStatus.Created, now.AddDays(-1)));

        var dto = await Create(db).GetJobsAsync(tenant, "7d", default);

        dto.SuccessRate.Should().Be(0);
    }

    // ---- Trend comparison vs prior period ----

    [Fact]
    public async Task GetJobs_trend_pct_compares_current_window_to_prior_window()
    {
        var tenant = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using var db = DashboardTestDbContext.Create();
        // Current window (last 7d): 4 jobs. Prior window (8..14 days ago): 2 jobs (priorFrom = now-14d).
        await DashboardTestDbContext.SeedAsync(db,
            Job(tenant, IntegrationJobStatus.Completed, now.AddDays(-1)),
            Job(tenant, IntegrationJobStatus.Completed, now.AddDays(-1)),
            Job(tenant, IntegrationJobStatus.Completed, now.AddDays(-1)),
            Job(tenant, IntegrationJobStatus.Completed, now.AddDays(-1)),
            Job(tenant, IntegrationJobStatus.Completed, now.AddDays(-10)),
            Job(tenant, IntegrationJobStatus.Completed, now.AddDays(-10)));

        var dto = await Create(db).GetJobsAsync(tenant, "7d", default);

        dto.Kpis.Total.Should().Be(4);
        // ((4 - 2) / 2) * 100 = 100
        dto.Kpis.TotalTrendPct.Should().Be(100);
        dto.Kpis.CompletedTrendPct.Should().Be(100);
    }

    [Fact]
    public async Task GetJobs_trend_pct_is_zero_when_prior_window_is_empty()
    {
        var tenant = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using var db = DashboardTestDbContext.Create();
        await DashboardTestDbContext.SeedAsync(db, Job(tenant, IntegrationJobStatus.Completed, now.AddDays(-1)));

        var dto = await Create(db).GetJobsAsync(tenant, "7d", default);

        dto.Kpis.TotalTrendPct.Should().Be(0, "trend is 0 when prior period count is 0");
    }

    // ---- Date-range filtering ----

    [Fact]
    public async Task GetJobs_7d_window_excludes_jobs_outside_the_last_7_days()
    {
        var tenant = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using var db = DashboardTestDbContext.Create();
        await DashboardTestDbContext.SeedAsync(db,
            Job(tenant, IntegrationJobStatus.Completed, now.AddDays(-2)),   // in window
            Job(tenant, IntegrationJobStatus.Completed, now.AddDays(-20))); // out of window

        var dto = await Create(db).GetJobsAsync(tenant, "7d", default);

        dto.Kpis.Total.Should().Be(1);
    }

    [Fact]
    public async Task GetJobs_today_window_only_includes_jobs_from_today()
    {
        var tenant = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using var db = DashboardTestDbContext.Create();
        await DashboardTestDbContext.SeedAsync(db,
            Job(tenant, IntegrationJobStatus.Completed, now),                    // today
            Job(tenant, IntegrationJobStatus.Completed, now.Date.AddHours(-1))); // yesterday

        var dto = await Create(db).GetJobsAsync(tenant, "today", default);

        dto.Kpis.Total.Should().Be(1);
    }

    [Fact]
    public async Task GetJobs_30d_window_includes_jobs_within_30_days()
    {
        var tenant = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using var db = DashboardTestDbContext.Create();
        await DashboardTestDbContext.SeedAsync(db,
            Job(tenant, IntegrationJobStatus.Completed, now.AddDays(-20))); // in 30d, not in 7d

        var sevenDay = await Create(db).GetJobsAsync(tenant, "7d", default);
        var thirtyDay = await Create(db).GetJobsAsync(tenant, "30d", default);

        sevenDay.Kpis.Total.Should().Be(0);
        thirtyDay.Kpis.Total.Should().Be(1);
    }

    [Fact]
    public async Task GetJobs_scopes_to_the_requested_tenant_only()
    {
        var tenant = Guid.NewGuid();
        var other = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using var db = DashboardTestDbContext.Create();
        await DashboardTestDbContext.SeedAsync(db,
            Job(tenant, IntegrationJobStatus.Completed, now.AddDays(-1)),
            Job(other, IntegrationJobStatus.Completed, now.AddDays(-1)));

        var dto = await Create(db).GetJobsAsync(tenant, "7d", default);

        dto.Kpis.Total.Should().Be(1);
    }

    [Fact]
    public async Task GetJobs_with_null_scope_aggregates_across_all_tenants()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using var db = DashboardTestDbContext.Create();
        await DashboardTestDbContext.SeedAsync(db,
            Job(tenantA, IntegrationJobStatus.Completed, now.AddDays(-1)),
            Job(tenantB, IntegrationJobStatus.Failed, now.AddDays(-1)));

        // Null tenant => cross-tenant view (the platform fan-out scopes the same way).
        var dto = await Create(db).GetJobsAsync(null, "7d", default);

        dto.Kpis.Total.Should().Be(2);
        dto.Kpis.Completed.Should().Be(1);
        dto.Kpis.Failed.Should().Be(1);
    }

    // ---- SLA breach detection (customer ageing) ----

    [Fact]
    public async Task GetCustomers_marks_sla_breach_when_open_request_idle_more_than_three_days()
    {
        var tenant = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using var db = DashboardTestDbContext.Create();
        // Open (PendingApproval), last updated 5 days ago -> breach.
        await DashboardTestDbContext.SeedAsync(db,
            Customer(tenant, CustomerRequestStatus.PendingApproval, now.AddDays(-6), now.AddDays(-5)));

        var dto = await Create(db).GetCustomersAsync(tenant, "7d", default);

        dto.Ageing.Should().HaveCount(1);
        dto.Ageing[0].SlaBreach.Should().BeTrue();
        dto.Ageing[0].DaysInStatus.Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public async Task GetCustomers_does_not_mark_sla_breach_within_three_days()
    {
        var tenant = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using var db = DashboardTestDbContext.Create();
        // Open, updated 1 day ago -> no breach.
        await DashboardTestDbContext.SeedAsync(db,
            Customer(tenant, CustomerRequestStatus.PendingApproval, now.AddDays(-2), now.AddDays(-1)));

        var dto = await Create(db).GetCustomersAsync(tenant, "7d", default);

        dto.Ageing.Should().HaveCount(1);
        dto.Ageing[0].SlaBreach.Should().BeFalse();
    }

    // ---- Cross-tenant platform aggregation ----

    // Cross-tenant platform fan-out. Previously the "repeated returns" computation stacked a second
    // GroupBy on a grouping result, which neither SQL Server nor the InMemory provider could translate;
    // it now materialises the per-request tenant ids (HAVING COUNT > 1) and rolls them up in memory.
    [Fact]
    public async Task GetPlatform_aggregates_across_active_tenants()
    {
        var tenantA = new Tenant { Id = Guid.NewGuid(), Name = "Tenant A", Identifier = "a", Status = TenantStatus.Active, CreatedDate = DateTime.UtcNow.AddDays(-30) };
        var tenantB = new Tenant { Id = Guid.NewGuid(), Name = "Tenant B", Identifier = "b", Status = TenantStatus.Active, CreatedDate = DateTime.UtcNow.AddDays(-30) };
        var inactive = new Tenant { Id = Guid.NewGuid(), Name = "Tenant C", Identifier = "c", Status = TenantStatus.Inactive, CreatedDate = DateTime.UtcNow.AddDays(-30) };
        _tenants.Setup(t => t.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { tenantA, tenantB, inactive });

        var now = DateTime.UtcNow;
        await using var db = DashboardTestDbContext.Create();
        await DashboardTestDbContext.SeedAsync(db,
            Job(tenantA.Id, IntegrationJobStatus.Completed, now.AddDays(-1)),
            Job(tenantA.Id, IntegrationJobStatus.Failed, now.AddDays(-1)),
            Job(tenantB.Id, IntegrationJobStatus.Completed, now.AddDays(-1)));

        var dto = await Create(db).GetPlatformAsync("7d", forceRefresh: false, default);

        dto.TenantKpis.ActiveTenants.Should().Be(2);
        dto.TenantKpis.InactiveTenants.Should().Be(1);
        dto.CrossTenantJobs.Should().HaveCount(2, "one row per tenant that has jobs in the window");
        dto.CrossTenantJobs.Should().Contain(j => j.TenantId == tenantA.Id && j.Completed == 1 && j.Failed == 1);
        dto.CrossTenantJobs.Should().Contain(j => j.TenantId == tenantB.Id && j.Completed == 1);
    }
}

/// <summary>Minimal <see cref="HealthCheckService"/> stub returning a healthy, empty report.</summary>
internal sealed class NoopHealthCheckService : HealthCheckService
{
    public override Task<HealthReport> CheckHealthAsync(
        Func<HealthCheckRegistration, bool>? predicate, CancellationToken cancellationToken = default)
        => Task.FromResult(new HealthReport(
            new Dictionary<string, HealthReportEntry>(), TimeSpan.Zero));
}
