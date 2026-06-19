using IntegrationHub.Api.Integrations;
using IntegrationHub.Api.Models.Dashboard;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IntegrationHub.Api.Dashboard;

/// <summary>
/// Builds the dashboard read models. All time windows are UTC. Queries bypass the ambient tenant
/// filter and scope explicitly by tenant id (or not at all, for the platform view) so a single
/// service serves tenant-scoped and cross-tenant callers. Missing source data degrades to 0/empty.
/// </summary>
public sealed class DashboardQueryService : IDashboardQueryService
{
    private const int SlaBreachDays = 3;

    private readonly IntegrationHubDbContext _db;
    private readonly IRetryQueueRepository _retries;
    private readonly ITenantRepository _tenants;
    private readonly ITenantApiConfigurationRepository _tenantConfigs;
    private readonly IJobScheduleConfigurationRepository _schedules;
    private readonly HealthCheckService _healthChecks;

    public DashboardQueryService(
        IntegrationHubDbContext db,
        IRetryQueueRepository retries,
        ITenantRepository tenants,
        ITenantApiConfigurationRepository tenantConfigs,
        IJobScheduleConfigurationRepository schedules,
        HealthCheckService healthChecks)
    {
        _db = db;
        _retries = retries;
        _tenants = tenants;
        _tenantConfigs = tenantConfigs;
        _schedules = schedules;
        _healthChecks = healthChecks;
    }

    // ---- Date windows ----

    private static (DateTime From, DateTime To, DateTime PriorFrom) Window(string? dateRange)
    {
        var now = DateTime.UtcNow;
        var to = now;
        DateTime from = dateRange switch
        {
            "today" => now.Date,
            "30d" => now.AddDays(-30),
            "90d" => now.AddDays(-90),
            _ => now.AddDays(-7),
        };
        var length = to - from;
        var priorFrom = from - length;
        return (from, to, priorFrom);
    }

    private static double TrendPct(int current, int prior)
        => prior == 0 ? 0 : Math.Round((double)(current - prior) / prior * 100, 0);

    private static IReadOnlyList<DateTime> DayBuckets(DateTime from, DateTime to)
    {
        var days = new List<DateTime>();
        for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
        {
            days.Add(d);
        }
        return days;
    }

    // ---- Job querying ----

    private IQueryable<IntegrationJob> JobsScoped(Guid? tenantId)
    {
        var q = _db.IntegrationJobs.IgnoreQueryFilters().Where(j => !j.Deleted);
        return tenantId is { } tid ? q.Where(j => j.TenantId == tid) : q;
    }

    private IQueryable<CustomerRequest> CustomersScoped(Guid? tenantId)
    {
        var q = _db.CustomerRequests.IgnoreQueryFilters().Where(c => !c.Deleted);
        return tenantId is { } tid ? q.Where(c => c.TenantId == tid) : q;
    }

    private IQueryable<CustomerAuditEntry> CustomerAuditScoped(Guid? tenantId)
    {
        var q = _db.CustomerAuditEntries.IgnoreQueryFilters().Where(a => !a.Deleted);
        return tenantId is { } tid ? q.Where(a => a.TenantId == tid) : q;
    }

    private IQueryable<User> UsersScoped(Guid? tenantId)
    {
        var q = _db.Users.IgnoreQueryFilters().Where(u => !u.Deleted);
        return tenantId is { } tid ? q.Where(u => u.TenantRoles.Any(r => r.TenantId == tid && !r.Deleted)) : q;
    }

    private static bool IsFailed(IntegrationJobStatus s) => s is IntegrationJobStatus.Failed or IntegrationJobStatus.PermanentlyFailed;

    private static bool IsPending(IntegrationJobStatus s) => s is IntegrationJobStatus.Created or IntegrationJobStatus.Running;

    public async Task<JobDashboardDto> GetJobsAsync(Guid? tenantId, string dateRange, CancellationToken cancellationToken)
    {
        var (from, to, priorFrom) = Window(dateRange);

        var current = await JobsScoped(tenantId)
            .Where(j => j.CreatedOnUtc >= from && j.CreatedOnUtc <= to)
            .Select(j => new { j.Id, j.Status, j.InterfaceName, j.SourceSystem, j.TargetSystem, j.CreatedOnUtc, j.CompletedAtUtc, j.UpdatedOnUtc, j.ErrorMessage })
            .ToListAsync(cancellationToken);

        var prior = await JobsScoped(tenantId)
            .Where(j => j.CreatedOnUtc >= priorFrom && j.CreatedOnUtc < from)
            .Select(j => new { j.Status })
            .ToListAsync(cancellationToken);

        int Completed(IEnumerable<IntegrationJobStatus> s) => s.Count(x => x == IntegrationJobStatus.Completed);

        var total = current.Count;
        var completed = current.Count(j => j.Status == IntegrationJobStatus.Completed);
        var failed = current.Count(j => IsFailed(j.Status));
        var pending = current.Count(j => IsPending(j.Status));

        var pTotal = prior.Count;
        var pCompleted = prior.Count(j => j.Status == IntegrationJobStatus.Completed);
        var pFailed = prior.Count(j => IsFailed(j.Status));
        var pPending = prior.Count(j => IsPending(j.Status));

        var kpis = new JobKpisDto(
            total, completed, failed, pending,
            TrendPct(total, pTotal), TrendPct(completed, pCompleted), TrendPct(failed, pFailed), TrendPct(pending, pPending));

        var successRate = (completed + failed) == 0 ? 0 : Math.Round((double)completed / (completed + failed) * 100, 1);

        var volume = DayBuckets(from, to).Select(day =>
        {
            var dayJobs = current.Where(j => j.CreatedOnUtc.Date == day).ToList();
            return new DailyJobCount(
                day.ToString("yyyy-MM-dd"),
                dayJobs.Count(j => j.Status == IntegrationJobStatus.Completed),
                dayJobs.Count(j => IsFailed(j.Status)),
                dayJobs.Count(j => IsPending(j.Status)));
        }).ToList();

        var flowBreakdown = IntegrationFlows.All().Select(flow =>
        {
            var flowJobs = current.Where(j => string.Equals(j.InterfaceName, flow.InterfaceName, StringComparison.OrdinalIgnoreCase)).ToList();
            return new FlowJobCount(
                flow.InterfaceName, flow.Label,
                flowJobs.Count(j => j.Status == IntegrationJobStatus.Completed),
                flowJobs.Count(j => IsFailed(j.Status)),
                flowJobs.Count(j => IsPending(j.Status)));
        }).ToList();

        var failedJobs = current
            .Where(j => IsFailed(j.Status))
            .OrderByDescending(j => j.CompletedAtUtc ?? j.UpdatedOnUtc)
            .Take(5)
            .Select(j => new FailedJobSummary(
                j.Id, j.InterfaceName,
                IntegrationFlows.Label(j.SourceSystem, j.TargetSystem, j.InterfaceName),
                j.ErrorMessage, j.CompletedAtUtc ?? j.UpdatedOnUtc))
            .ToList();

        var (retryCount, nextRun) = await RetryQueueAsync(tenantId, cancellationToken);

        return new JobDashboardDto(kpis, successRate, volume, flowBreakdown, failedJobs, retryCount, nextRun);
    }

    private async Task<(int Count, DateTime? NextRun)> RetryQueueAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        try
        {
            var (items, total) = await _retries.QueryAsync(tenantId, 1, 100, cancellationToken);
            var next = items.Count == 0 ? (DateTime?)null : items.Min(r => r.NextRetryDate);
            return (total, next);
        }
        catch
        {
            return (0, null);
        }
    }

    // ---- Health ----

    public async Task<HealthDashboardDto> GetHealthAsync(CancellationToken cancellationToken)
    {
        var report = await _healthChecks.CheckHealthAsync(cancellationToken);
        var components = report.Entries
            .Select(e => new HealthComponentDto(e.Key, e.Value.Status.ToString(), e.Value.Description))
            .ToList();
        var allOperational = report.Entries.All(e => e.Value.Status == HealthStatus.Healthy);
        return new HealthDashboardDto(report.Status.ToString(), components, allOperational);
    }

    // ---- Customers ----

    private static readonly CustomerRequestStatus[] PendingActionStatuses =
    {
        CustomerRequestStatus.Submitted, CustomerRequestStatus.UnderReview, CustomerRequestStatus.PendingApproval,
        CustomerRequestStatus.PartiallyApproved, CustomerRequestStatus.Returned,
    };

    public async Task<CustomerDashboardDto> GetCustomersAsync(Guid? tenantId, string dateRange, CancellationToken cancellationToken)
    {
        var (from, to, priorFrom) = Window(dateRange);

        var current = await CustomersScoped(tenantId)
            .Where(c => c.CreatedOnUtc >= from && c.CreatedOnUtc <= to)
            .Select(c => new { c.Status })
            .ToListAsync(cancellationToken);

        var prior = await CustomersScoped(tenantId)
            .Where(c => c.CreatedOnUtc >= priorFrom && c.CreatedOnUtc < from)
            .Select(c => new { c.Status })
            .ToListAsync(cancellationToken);

        int PendingAction(IEnumerable<CustomerRequestStatus> s) => s.Count(x => PendingActionStatuses.Contains(x));

        var total = current.Count;
        var synced = current.Count(c => c.Status == CustomerRequestStatus.Synced);
        var pendingAction = current.Count(c => PendingActionStatuses.Contains(c.Status));
        var syncFailed = current.Count(c => c.Status == CustomerRequestStatus.Failed);
        var rejected = current.Count(c => c.Status == CustomerRequestStatus.Rejected);

        var kpis = new CustomerKpisDto(
            total, synced, pendingAction, syncFailed, rejected,
            TrendPct(total, prior.Count),
            TrendPct(synced, prior.Count(c => c.Status == CustomerRequestStatus.Synced)));

        // Funnel: count per stage across the full (unwindowed) scoped set, ordered by enum.
        var byStatus = await CustomersScoped(tenantId)
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var funnel = Enum.GetValues<CustomerRequestStatus>()
            .Select(st => new StageCount(st.ToString(), byStatus.FirstOrDefault(x => x.Status == st)?.Count ?? 0))
            .ToList();

        // Ageing: open requests (not Synced/Rejected), oldest-in-status first, top 5.
        var openStatuses = Enum.GetValues<CustomerRequestStatus>()
            .Where(s => s != CustomerRequestStatus.Synced && s != CustomerRequestStatus.Rejected)
            .ToArray();
        var ageingRaw = await CustomersScoped(tenantId)
            .Where(c => openStatuses.Contains(c.Status))
            .Select(c => new { c.Id, c.CustomerRequestNumber, c.CompanyName, c.Status, c.UpdatedOnUtc })
            .ToListAsync(cancellationToken);
        var nowUtc = DateTime.UtcNow;
        var ageing = ageingRaw
            .Select(c =>
            {
                var days = (int)Math.Floor((nowUtc - c.UpdatedOnUtc).TotalDays);
                return new AgeingItem(c.Id, c.CustomerRequestNumber, c.CompanyName, c.Status.ToString(), days, days > SlaBreachDays);
            })
            .OrderByDescending(a => a.DaysInStatus)
            .Take(5)
            .ToList();

        var syncHealth = await SyncHealthAsync(tenantId, from, to, cancellationToken);
        var activityFeed = await CustomerActivityFeedAsync(tenantId, 15, cancellationToken);
        var topSubmitters = await TopSubmittersAsync(tenantId, cancellationToken);
        var submissionTrend = await SubmissionTrendAsync(tenantId, cancellationToken);

        return new CustomerDashboardDto(kpis, funnel, ageing, syncHealth, activityFeed, topSubmitters, submissionTrend);
    }

    private async Task<SyncHealthDto> SyncHealthAsync(Guid? tenantId, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var totalSynced = await CustomersScoped(tenantId).CountAsync(c => c.Status == CustomerRequestStatus.Synced, cancellationToken);
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var syncedThisMonth = await CustomersScoped(tenantId)
            .CountAsync(c => c.Status == CustomerRequestStatus.Synced && c.UpdatedOnUtc >= monthStart, cancellationToken);
        var inProgress = await CustomersScoped(tenantId).CountAsync(c => c.Status == CustomerRequestStatus.SyncInProgress, cancellationToken);
        var failed = await CustomersScoped(tenantId).CountAsync(c => c.Status == CustomerRequestStatus.Failed, cancellationToken);
        var successRate = (totalSynced + failed) == 0 ? 0 : Math.Round((double)totalSynced / (totalSynced + failed) * 100, 1);

        // Per-day Synced vs Failed timeline over the window (keyed on UpdatedOnUtc).
        var windowItems = await CustomersScoped(tenantId)
            .Where(c => (c.Status == CustomerRequestStatus.Synced || c.Status == CustomerRequestStatus.Failed)
                        && c.UpdatedOnUtc >= from && c.UpdatedOnUtc <= to)
            .Select(c => new { c.Status, c.UpdatedOnUtc })
            .ToListAsync(cancellationToken);
        var timeline = DayBuckets(from, to).Select(day => new SyncTimelinePoint(
            day.ToString("yyyy-MM-dd"),
            windowItems.Count(c => c.Status == CustomerRequestStatus.Synced && c.UpdatedOnUtc.Date == day),
            windowItems.Count(c => c.Status == CustomerRequestStatus.Failed && c.UpdatedOnUtc.Date == day))).ToList();

        var recentFailures = await CustomersScoped(tenantId)
            .Where(c => c.Status == CustomerRequestStatus.Failed)
            .OrderByDescending(c => c.UpdatedOnUtc)
            .Take(3)
            .Select(c => new SyncFailureItem(c.Id, c.CustomerRequestNumber, c.CompanyName, c.LastSyncError, c.UpdatedOnUtc))
            .ToListAsync(cancellationToken);

        return new SyncHealthDto(totalSynced, syncedThisMonth, successRate, inProgress, failed, timeline, recentFailures);
    }

    private async Task<IReadOnlyList<ActivityEntry>> CustomerActivityFeedAsync(Guid? tenantId, int take, CancellationToken cancellationToken)
    {
        var rows = await CustomerAuditScoped(tenantId)
            .OrderByDescending(a => a.PerformedOnUtc)
            .Take(take)
            .Select(a => new
            {
                a.Id, a.ActionType, a.PerformedBy, a.PerformedOnUtc, a.CustomerRequestId, a.Notes,
                Number = a.CustomerRequest != null ? a.CustomerRequest.CustomerRequestNumber : null,
            })
            .ToListAsync(cancellationToken);
        return rows.Select(a => new ActivityEntry(
            a.Id, a.ActionType.ToString(), a.PerformedBy, a.PerformedOnUtc, a.CustomerRequestId, a.Number, a.Notes)).ToList();
    }

    private async Task<IReadOnlyList<SubmitterCount>> TopSubmittersAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        var grouped = await CustomersScoped(tenantId)
            .Where(c => c.SubmittedById != null)
            .GroupBy(c => c.SubmittedById)
            .Select(g => new { SubmitterId = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(5)
            .ToListAsync(cancellationToken);

        if (grouped.Count == 0)
        {
            return Array.Empty<SubmitterCount>();
        }

        var names = await NamesAsync(grouped.Select(g => g.SubmitterId!.Value), cancellationToken);
        return grouped.Select(g => new SubmitterCount(
            g.SubmitterId,
            names.TryGetValue(g.SubmitterId!.Value, out var name) ? name : "Unknown",
            g.Count)).ToList();
    }

    private async Task<IReadOnlyList<SubmissionTrendPoint>> SubmissionTrendAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow.Date;
        var start = now.AddDays(-7 * 11); // 12 week-buckets including the current week.
        var submitted = await CustomersScoped(tenantId)
            .Where(c => c.SubmittedOnUtc != null && c.SubmittedOnUtc >= start)
            .Select(c => c.SubmittedOnUtc!.Value)
            .ToListAsync(cancellationToken);
        var approved = await CustomersScoped(tenantId)
            .Where(c => c.ApprovedOnUtc != null && c.ApprovedOnUtc >= start)
            .Select(c => c.ApprovedOnUtc!.Value)
            .ToListAsync(cancellationToken);

        var points = new List<SubmissionTrendPoint>();
        for (var i = 0; i < 12; i++)
        {
            var weekStart = start.AddDays(7 * i);
            var weekEnd = weekStart.AddDays(7);
            points.Add(new SubmissionTrendPoint(
                weekStart.ToString("yyyy-MM-dd"),
                submitted.Count(d => d >= weekStart && d < weekEnd),
                approved.Count(d => d >= weekStart && d < weekEnd)));
        }
        return points;
    }

    private async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }
        var users = await _db.Users.IgnoreQueryFilters()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new
            {
                u.Id, u.DisplayName, u.Email,
                FirstName = u.Person != null ? u.Person.FirstName : null,
                LastName = u.Person != null ? u.Person.LastName : null,
            })
            .ToListAsync(cancellationToken);
        return users.ToDictionary(u => u.Id, u =>
        {
            var name = string.Join(" ", new[] { u.FirstName, u.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
            return string.IsNullOrWhiteSpace(name) ? (string.IsNullOrWhiteSpace(u.DisplayName) ? u.Email : u.DisplayName) : name;
        });
    }

    // ---- Users ----

    public async Task<UserDashboardDto> GetUsersAsync(Guid? tenantId, string dateRange, CancellationToken cancellationToken)
    {
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var users = await UsersScoped(tenantId)
            .Select(u => new { u.Id, u.MustChangePassword, u.CreatedOnUtc, u.IsActive })
            .ToListAsync(cancellationToken);

        // LoggedInToday / ActiveThisWeek / Inactive30Days are not trackable (no last-login timestamp): return 0.
        var kpis = new UserKpisDto(
            Total: users.Count,
            LoggedInToday: 0,
            ActiveThisWeek: 0,
            Inactive30Days: 0,
            PendingFirstLogin: users.Count(u => u.MustChangePassword),
            NewThisMonth: users.Count(u => u.CreatedOnUtc >= monthStart));

        var roleDistribution = await RoleDistributionAsync(tenantId, cancellationToken);

        // No user-activity audit source; an empty feed is acceptable for this WO.
        return new UserDashboardDto(kpis, roleDistribution, Array.Empty<ActivityEntry>());
    }

    private async Task<IReadOnlyList<RoleCount>> RoleDistributionAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        var assignments = await _db.UserTenantRoles.IgnoreQueryFilters()
            .Where(r => !r.Deleted)
            .Where(r => tenantId == null || r.TenantId == tenantId)
            .Where(r => !r.User!.Deleted)
            .Select(r => new { r.UserId, RoleName = r.RoleEntity != null ? r.RoleEntity.Name : null, r.Role })
            .ToListAsync(cancellationToken);

        // One role label per user (first assignment wins); users with no assignment are "Unassigned".
        var allUsers = await UsersScoped(tenantId).Select(u => u.Id).ToListAsync(cancellationToken);
        var byUser = assignments
            .GroupBy(a => a.UserId)
            .ToDictionary(g => g.Key, g => g.First().RoleName ?? g.First().Role.ToString());

        var counts = new Dictionary<string, int>();
        foreach (var userId in allUsers)
        {
            var label = byUser.TryGetValue(userId, out var name) && !string.IsNullOrWhiteSpace(name) ? name : "Unassigned";
            counts[label] = counts.GetValueOrDefault(label) + 1;
        }
        return counts.Select(kv => new RoleCount(kv.Key, kv.Value)).OrderByDescending(r => r.Count).ToList();
    }

    // ---- Platform (Super Admin) ----

    public async Task<PlatformDashboardDto> GetPlatformAsync(string dateRange, bool forceRefresh, CancellationToken cancellationToken)
        => await BuildPlatformAsync(dateRange, cancellationToken);

    private async Task<PlatformDashboardDto> BuildPlatformAsync(string dateRange, CancellationToken cancellationToken)
    {
        var (from, to, _) = Window(dateRange);
        var todayStart = DateTime.UtcNow.Date;
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var tenants = await _tenants.ListAsync(cancellationToken);
        var tenantNames = tenants.ToDictionary(t => t.Id, t => t.Name);

        var activeTenants = tenants.Count(t => t.Status == TenantStatus.Active);
        var inactiveTenants = tenants.Count(t => t.Status == TenantStatus.Inactive);
        var archivedTenants = tenants.Count(t => t.Status == TenantStatus.Archived);

        var totalUsers = await _db.Users.IgnoreQueryFilters().CountAsync(u => !u.Deleted, cancellationToken);
        var jobsToday = await JobsScoped(null).CountAsync(j => j.CreatedOnUtc >= todayStart, cancellationToken);

        // Platform success rate over all jobs (completed / (completed + failed)).
        var platCompleted = await JobsScoped(null).CountAsync(j => j.Status == IntegrationJobStatus.Completed, cancellationToken);
        var platFailed = await JobsScoped(null).CountAsync(j => j.Status == IntegrationJobStatus.Failed || j.Status == IntegrationJobStatus.PermanentlyFailed, cancellationToken);
        var platSuccessRate = (platCompleted + platFailed) == 0 ? 0 : Math.Round((double)platCompleted / (platCompleted + platFailed) * 100, 1);

        var pendingApprovals = await CustomersScoped(null)
            .CountAsync(c => c.Status == CustomerRequestStatus.PendingApproval || c.Status == CustomerRequestStatus.PartiallyApproved, cancellationToken);

        var tenantKpis = new TenantKpisDto(
            activeTenants, inactiveTenants, archivedTenants, totalUsers, jobsToday, platSuccessRate, pendingApprovals);

        // Cross-tenant jobs (window), top 10 by volume.
        var jobsByTenant = await JobsScoped(null)
            .Where(j => j.CreatedOnUtc >= from && j.CreatedOnUtc <= to)
            .GroupBy(j => j.TenantId)
            .Select(g => new
            {
                TenantId = g.Key,
                Completed = g.Count(j => j.Status == IntegrationJobStatus.Completed),
                Failed = g.Count(j => j.Status == IntegrationJobStatus.Failed || j.Status == IntegrationJobStatus.PermanentlyFailed),
                Pending = g.Count(j => j.Status == IntegrationJobStatus.Created || j.Status == IntegrationJobStatus.Running),
                Total = g.Count(),
            })
            .ToListAsync(cancellationToken);
        var crossTenantJobs = jobsByTenant
            .OrderByDescending(g => g.Total)
            .Take(10)
            .Select(g => new CrossTenantJobCount(
                g.TenantId, tenantNames.GetValueOrDefault(g.TenantId, g.TenantId.ToString()), g.Completed, g.Failed, g.Pending))
            .ToList();

        var tenantHealth = await TenantHealthAsync(tenants, cancellationToken);
        var growth = await GrowthAsync(tenants, cancellationToken);
        var onboarding = await OnboardingAsync(tenants, cancellationToken);
        var userAnalytics = await PlatformUserAnalyticsAsync(tenantNames, monthStart, cancellationToken);
        var customer = await PlatformCustomerAsync(dateRange, tenantNames, cancellationToken);
        var alerts = await SystemAlertsAsync(tenantNames, customer, crossTenantJobs, cancellationToken);

        return new PlatformDashboardDto(tenantKpis, crossTenantJobs, tenantHealth, growth, onboarding, alerts, userAnalytics, customer);
    }

    private async Task<IReadOnlyList<TenantHealthRow>> TenantHealthAsync(IReadOnlyList<Tenant> tenants, CancellationToken cancellationToken)
    {
        var rows = new List<TenantHealthRow>();
        foreach (var t in tenants)
        {
            var configs = await _tenantConfigs.ListByTenantAsync(t.Id, cancellationToken);
            var concur = configs.Any(c => c.System == SystemName.Concur && !string.IsNullOrWhiteSpace(c.EncryptedCredentials));
            var maconomy = configs.Any(c => c.System == SystemName.Maconomy && !string.IsNullOrWhiteSpace(c.EncryptedCredentials));

            var lastRun = await JobsScoped(t.Id).MaxAsync(j => (DateTime?)(j.CompletedAtUtc ?? j.CreatedOnUtc), cancellationToken);
            var completed = await JobsScoped(t.Id).CountAsync(j => j.Status == IntegrationJobStatus.Completed, cancellationToken);
            var failed = await JobsScoped(t.Id).CountAsync(j => j.Status == IntegrationJobStatus.Failed || j.Status == IntegrationJobStatus.PermanentlyFailed, cancellationToken);
            var successRate = (completed + failed) == 0 ? 0 : Math.Round((double)completed / (completed + failed) * 100, 1);
            var pendingCustomers = await CustomersScoped(t.Id)
                .CountAsync(c => c.Status == CustomerRequestStatus.PendingApproval || c.Status == CustomerRequestStatus.PartiallyApproved, cancellationToken);
            var activeUsers = await UsersScoped(t.Id).CountAsync(u => u.IsActive, cancellationToken);

            rows.Add(new TenantHealthRow(t.Id, t.Name, concur, maconomy, lastRun, successRate, pendingCustomers, activeUsers));
        }
        return rows;
    }

    private async Task<IReadOnlyList<GrowthPoint>> GrowthAsync(IReadOnlyList<Tenant> tenants, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow.Date;
        var start = now.AddDays(-90);
        var userDates = await _db.Users.IgnoreQueryFilters().Where(u => !u.Deleted).Select(u => u.CreatedOnUtc).ToListAsync(cancellationToken);
        var tenantDates = tenants.Select(t => t.CreatedDate == default ? t.CreatedOnUtc : t.CreatedDate).ToList();

        var points = new List<GrowthPoint>();
        for (var d = start; d <= now; d = d.AddDays(1))
        {
            var dayEnd = d.AddDays(1);
            points.Add(new GrowthPoint(
                d.ToString("yyyy-MM-dd"),
                tenantDates.Count(td => td < dayEnd),
                userDates.Count(ud => ud < dayEnd)));
        }
        return points;
    }

    private async Task<IReadOnlyList<OnboardingRow>> OnboardingAsync(IReadOnlyList<Tenant> tenants, CancellationToken cancellationToken)
    {
        var schedules = await _schedules.ListAsync(cancellationToken);
        var rows = new List<OnboardingRow>();
        foreach (var t in tenants)
        {
            var configs = await _tenantConfigs.ListByTenantAsync(t.Id, cancellationToken);
            var missingCreds = !configs.Any(c => !string.IsNullOrWhiteSpace(c.EncryptedCredentials));
            var missingUsers = !await UsersScoped(t.Id).AnyAsync(cancellationToken);
            var missingSchedules = !schedules.Any(s => s.TenantId == t.Id);
            rows.Add(new OnboardingRow(t.Id, t.Name, missingCreds, missingUsers, missingSchedules));
        }
        return rows;
    }

    private async Task<PlatformUserAnalyticsDto> PlatformUserAnalyticsAsync(
        IReadOnlyDictionary<Guid, string> tenantNames, DateTime monthStart, CancellationToken cancellationToken)
    {
        var totalActive = await _db.Users.IgnoreQueryFilters().CountAsync(u => !u.Deleted && u.IsActive, cancellationToken);
        var pendingFirstLogin = await _db.Users.IgnoreQueryFilters().CountAsync(u => !u.Deleted && u.MustChangePassword, cancellationToken);
        var newThisMonth = await _db.Users.IgnoreQueryFilters().CountAsync(u => !u.Deleted && u.CreatedOnUtc >= monthStart, cancellationToken);

        // Users with no (non-deleted) tenant-role assignment.
        var noRole = await _db.Users.IgnoreQueryFilters()
            .CountAsync(u => !u.Deleted && !u.TenantRoles.Any(r => !r.Deleted), cancellationToken);

        // ByTenant: count of users per tenant via assignments.
        var byTenantRaw = await _db.UserTenantRoles.IgnoreQueryFilters()
            .Where(r => !r.Deleted && !r.User!.Deleted)
            .GroupBy(r => r.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Select(x => x.UserId).Distinct().Count() })
            .ToListAsync(cancellationToken);
        var byTenant = byTenantRaw
            .Select(x => new TenantCount(tenantNames.GetValueOrDefault(x.TenantId, x.TenantId.ToString()), x.Count))
            .OrderByDescending(x => x.Count)
            .ToList();

        var growth = await GrowthAsync(await _tenants.ListAsync(cancellationToken), cancellationToken);

        // LoggedInToday not trackable (no last-login timestamp): 0. ActivityFeed empty (no user audit source).
        return new PlatformUserAnalyticsDto(
            totalActive, LoggedInToday: 0, pendingFirstLogin, noRole, newThisMonth, growth, byTenant, Array.Empty<ActivityEntry>());
    }

    private async Task<PlatformCustomerDto> PlatformCustomerAsync(
        string dateRange, IReadOnlyDictionary<Guid, string> tenantNames, CancellationToken cancellationToken)
    {
        var (from, to, priorFrom) = Window(dateRange);

        var current = await CustomersScoped(null)
            .Where(c => c.CreatedOnUtc >= from && c.CreatedOnUtc <= to)
            .Select(c => new { c.Status })
            .ToListAsync(cancellationToken);
        var priorCount = await CustomersScoped(null).CountAsync(c => c.CreatedOnUtc >= priorFrom && c.CreatedOnUtc < from, cancellationToken);

        var total = current.Count;
        var synced = current.Count(c => c.Status == CustomerRequestStatus.Synced);
        var pendingApproval = current.Count(c => c.Status == CustomerRequestStatus.PendingApproval || c.Status == CustomerRequestStatus.PartiallyApproved);
        var syncFailed = current.Count(c => c.Status == CustomerRequestStatus.Failed);
        var rejected = current.Count(c => c.Status == CustomerRequestStatus.Rejected);

        var byTenantRaw = await CustomersScoped(null)
            .GroupBy(c => c.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var byTenant = byTenantRaw
            .Select(x => new TenantCount(tenantNames.GetValueOrDefault(x.TenantId, x.TenantId.ToString()), x.Count))
            .OrderByDescending(x => x.Count)
            .ToList();

        var windowSync = await CustomersScoped(null)
            .Where(c => (c.Status == CustomerRequestStatus.Synced || c.Status == CustomerRequestStatus.Failed)
                        && c.UpdatedOnUtc >= from && c.UpdatedOnUtc <= to)
            .Select(c => new { c.Status, c.UpdatedOnUtc })
            .ToListAsync(cancellationToken);
        var syncTimeline = DayBuckets(from, to).Select(day => new SyncTimelinePoint(
            day.ToString("yyyy-MM-dd"),
            windowSync.Count(c => c.Status == CustomerRequestStatus.Synced && c.UpdatedOnUtc.Date == day),
            windowSync.Count(c => c.Status == CustomerRequestStatus.Failed && c.UpdatedOnUtc.Date == day))).ToList();

        var byStatus = await CustomersScoped(null)
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var funnel = Enum.GetValues<CustomerRequestStatus>()
            .Select(st => new StageCount(st.ToString(), byStatus.FirstOrDefault(x => x.Status == st)?.Count ?? 0))
            .ToList();

        var issues = await CustomerIssuesAsync(tenantNames, cancellationToken);

        return new PlatformCustomerDto(
            total, synced, pendingApproval, syncFailed, rejected, TrendPct(total, priorCount),
            byTenant, syncTimeline, funnel, issues);
    }

    private async Task<IReadOnlyList<CustomerIssueRow>> CustomerIssuesAsync(
        IReadOnlyDictionary<Guid, string> tenantNames, CancellationToken cancellationToken)
    {
        var staleCutoff = DateTime.UtcNow.AddDays(-SlaBreachDays);

        var staleApprovals = await CustomersScoped(null)
            .Where(c => (c.Status == CustomerRequestStatus.PendingApproval || c.Status == CustomerRequestStatus.PartiallyApproved)
                        && c.UpdatedOnUtc < staleCutoff)
            .GroupBy(c => c.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var syncFailures = await CustomersScoped(null)
            .Where(c => c.Status == CustomerRequestStatus.Failed)
            .GroupBy(c => c.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        // Repeated returns: requests with more than one Returned audit action, grouped by tenant.
        // The per-request HAVING COUNT > 1 grouping translates to SQL; the second roll-up by tenant is
        // not translatable on top of a grouping result, so materialise the per-request tenant ids first
        // and count them per tenant in memory.
        var repeatedReturnTenantIds = await CustomerAuditScoped(null)
            .Where(a => a.ActionType == CustomerAuditActionType.Returned)
            .GroupBy(a => new { a.TenantId, a.CustomerRequestId })
            .Where(g => g.Count() > 1)
            .Select(g => g.Key.TenantId)
            .ToListAsync(cancellationToken);
        var repeatedReturns = repeatedReturnTenantIds
            .GroupBy(t => t)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToList();

        var tenantIds = staleApprovals.Select(x => x.TenantId)
            .Concat(syncFailures.Select(x => x.TenantId))
            .Concat(repeatedReturns.Select(x => x.TenantId))
            .Distinct()
            .ToList();

        return tenantIds.Select(tid => new CustomerIssueRow(
            tid, tenantNames.GetValueOrDefault(tid, tid.ToString()),
            staleApprovals.FirstOrDefault(x => x.TenantId == tid)?.Count ?? 0,
            syncFailures.FirstOrDefault(x => x.TenantId == tid)?.Count ?? 0,
            repeatedReturns.FirstOrDefault(x => x.TenantId == tid)?.Count ?? 0)).ToList();
    }

    private Task<IReadOnlyList<SystemAlert>> SystemAlertsAsync(
        IReadOnlyDictionary<Guid, string> tenantNames,
        PlatformCustomerDto customer,
        IReadOnlyList<CrossTenantJobCount> crossTenantJobs,
        CancellationToken cancellationToken)
    {
        var alerts = new List<SystemAlert>();

        // Synthesize from sync failures per tenant.
        foreach (var issue in customer.Issues.Where(i => i.SyncFailures > 0))
        {
            alerts.Add(new SystemAlert(
                $"syncfail:{issue.TenantId}", "SyncFailure",
                issue.SyncFailures >= 5 ? "High" : "Warning",
                $"{issue.SyncFailures} customer sync failure(s) awaiting attention.",
                issue.TenantId, issue.TenantName));
        }

        // Synthesize from failed-job spikes per tenant in the window.
        foreach (var job in crossTenantJobs.Where(j => j.Failed > 0))
        {
            alerts.Add(new SystemAlert(
                $"jobfail:{job.TenantId}", "JobFailure",
                job.Failed >= 5 ? "High" : "Warning",
                $"{job.Failed} failed integration job(s) in the selected window.",
                job.TenantId, job.TenantName));
        }

        return Task.FromResult<IReadOnlyList<SystemAlert>>(alerts);
    }
}
