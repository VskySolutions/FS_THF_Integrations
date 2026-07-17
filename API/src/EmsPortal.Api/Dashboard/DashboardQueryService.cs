using EmsPortal.Api.Models.Dashboard;
using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;
using EmsPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EmsPortal.Api.Dashboard;

/// <summary>
/// Builds the dashboard read models. All time windows are UTC. Queries bypass the ambient tenant
/// filter and scope explicitly by tenant id (or not at all, for the platform view) so a single
/// service serves tenant-scoped and cross-tenant callers. Missing source data degrades to 0/empty.
/// </summary>
public sealed class DashboardQueryService : IDashboardQueryService
{
    private const int SlaBreachDays = 3;

    private readonly EmsPortalDbContext _db;
    private readonly ITenantRepository _tenants;
    private readonly IUserRepository _users;

    public DashboardQueryService(
        EmsPortalDbContext db,
        ITenantRepository tenants,
        IUserRepository users)
    {
        _db = db;
        _tenants = tenants;
        _users = users;
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

    // ---- Scoping helpers ----

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

        var total = current.Count;
        var approved = current.Count(c => c.Status == CustomerRequestStatus.Approved);
        var pendingAction = current.Count(c => PendingActionStatuses.Contains(c.Status));
        var rejected = current.Count(c => c.Status == CustomerRequestStatus.Rejected);

        var kpis = new CustomerKpisDto(
            total, approved, pendingAction, rejected,
            TrendPct(total, prior.Count),
            TrendPct(approved, prior.Count(c => c.Status == CustomerRequestStatus.Approved)));

        // Funnel: count per stage across the full (unwindowed) scoped set, ordered by enum.
        var byStatus = await CustomersScoped(tenantId)
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var funnel = Enum.GetValues<CustomerRequestStatus>()
            .Select(st => new StageCount(st.ToString(), byStatus.FirstOrDefault(x => x.Status == st)?.Count ?? 0))
            .ToList();

        // Ageing: open requests (not Approved/Rejected), oldest-in-status first, top 5.
        var openStatuses = Enum.GetValues<CustomerRequestStatus>()
            .Where(s => s != CustomerRequestStatus.Approved && s != CustomerRequestStatus.Rejected)
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

        var activityFeed = await CustomerActivityFeedAsync(tenantId, 15, cancellationToken);
        var topSubmitters = await TopSubmittersAsync(tenantId, cancellationToken);
        var submissionTrend = await SubmissionTrendAsync(tenantId, cancellationToken);

        return new CustomerDashboardDto(kpis, funnel, ageing, activityFeed, topSubmitters, submissionTrend);
    }

    private async Task<IReadOnlyList<ActivityEntry>> CustomerActivityFeedAsync(Guid? tenantId, int take, CancellationToken cancellationToken)
    {
        var rows = await CustomerAuditScoped(tenantId)
            .OrderByDescending(a => a.PerformedOnUtc)
            .Take(take)
            .Select(a => new
            {
                a.Id, a.ActionType, a.PerformedById, a.PerformedBy, a.PerformedOnUtc, a.CustomerRequestId, a.Notes,
                Number = a.CustomerRequest != null ? a.CustomerRequest.CustomerRequestNumber : null,
            })
            .ToListAsync(cancellationToken);

        // Resolve actor ids to display names (the denormalised PerformedBy can hold an id, never shown raw).
        var actorNames = await _users.GetFullNamesAsync(
            rows.Where(a => a.PerformedById.HasValue).Select(a => a.PerformedById!.Value), cancellationToken);

        return rows.Select(a => new ActivityEntry(
            a.Id,
            a.ActionType.ToString(),
            a.PerformedById is { } pid && actorNames.TryGetValue(pid, out var name) ? name : null,
            a.PerformedOnUtc,
            a.CustomerRequestId,
            a.Number,
            a.Notes)).ToList();
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
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var tenants = await _tenants.ListAsync(cancellationToken);
        var tenantNames = tenants.ToDictionary(t => t.Id, t => t.Name);

        var activeTenants = tenants.Count(t => t.Status == TenantStatus.Active);
        var inactiveTenants = tenants.Count(t => t.Status == TenantStatus.Inactive);
        var archivedTenants = tenants.Count(t => t.Status == TenantStatus.Archived);

        var totalUsers = await _db.Users.IgnoreQueryFilters().CountAsync(u => !u.Deleted, cancellationToken);

        var pendingApprovals = await CustomersScoped(null)
            .CountAsync(c => c.Status == CustomerRequestStatus.PendingApproval || c.Status == CustomerRequestStatus.PartiallyApproved, cancellationToken);

        var tenantKpis = new TenantKpisDto(
            activeTenants, inactiveTenants, archivedTenants, totalUsers, pendingApprovals);

        var tenantHealth = await TenantHealthAsync(tenants, cancellationToken);
        var growth = await GrowthAsync(tenants, cancellationToken);
        var onboarding = await OnboardingAsync(tenants, cancellationToken);
        var userAnalytics = await PlatformUserAnalyticsAsync(tenantNames, monthStart, cancellationToken);
        var customer = await PlatformCustomerAsync(dateRange, tenantNames, cancellationToken);
        var alerts = await SystemAlertsAsync(customer, cancellationToken);

        return new PlatformDashboardDto(tenantKpis, tenantHealth, growth, onboarding, alerts, userAnalytics, customer);
    }

    private async Task<IReadOnlyList<TenantHealthRow>> TenantHealthAsync(IReadOnlyList<Tenant> tenants, CancellationToken cancellationToken)
    {
        var rows = new List<TenantHealthRow>();
        foreach (var t in tenants)
        {
            var pendingCustomers = await CustomersScoped(t.Id)
                .CountAsync(c => c.Status == CustomerRequestStatus.PendingApproval || c.Status == CustomerRequestStatus.PartiallyApproved, cancellationToken);
            var activeUsers = await UsersScoped(t.Id).CountAsync(u => u.IsActive, cancellationToken);

            rows.Add(new TenantHealthRow(t.Id, t.Name, pendingCustomers, activeUsers));
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
        var rows = new List<OnboardingRow>();
        foreach (var t in tenants)
        {
            var missingUsers = !await UsersScoped(t.Id).AnyAsync(cancellationToken);
            rows.Add(new OnboardingRow(t.Id, t.Name, missingUsers));
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
        var approved = current.Count(c => c.Status == CustomerRequestStatus.Approved);
        var pendingApproval = current.Count(c => c.Status == CustomerRequestStatus.PendingApproval || c.Status == CustomerRequestStatus.PartiallyApproved);
        var rejected = current.Count(c => c.Status == CustomerRequestStatus.Rejected);

        var byTenantRaw = await CustomersScoped(null)
            .GroupBy(c => c.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var byTenant = byTenantRaw
            .Select(x => new TenantCount(tenantNames.GetValueOrDefault(x.TenantId, x.TenantId.ToString()), x.Count))
            .OrderByDescending(x => x.Count)
            .ToList();

        var byStatus = await CustomersScoped(null)
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var funnel = Enum.GetValues<CustomerRequestStatus>()
            .Select(st => new StageCount(st.ToString(), byStatus.FirstOrDefault(x => x.Status == st)?.Count ?? 0))
            .ToList();

        var issues = await CustomerIssuesAsync(tenantNames, cancellationToken);

        return new PlatformCustomerDto(
            total, approved, pendingApproval, rejected, TrendPct(total, priorCount),
            byTenant, funnel, issues);
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
            .Concat(repeatedReturns.Select(x => x.TenantId))
            .Distinct()
            .ToList();

        return tenantIds.Select(tid => new CustomerIssueRow(
            tid, tenantNames.GetValueOrDefault(tid, tid.ToString()),
            staleApprovals.FirstOrDefault(x => x.TenantId == tid)?.Count ?? 0,
            repeatedReturns.FirstOrDefault(x => x.TenantId == tid)?.Count ?? 0)).ToList();
    }

    private Task<IReadOnlyList<SystemAlert>> SystemAlertsAsync(
        PlatformCustomerDto customer,
        CancellationToken cancellationToken)
    {
        var alerts = new List<SystemAlert>();

        // Synthesize from stale customer approvals per tenant.
        foreach (var issue in customer.Issues.Where(i => i.StaleApprovals > 0))
        {
            alerts.Add(new SystemAlert(
                $"staleapproval:{issue.TenantId}", "StaleApprovals",
                issue.StaleApprovals >= 5 ? "High" : "Warning",
                $"{issue.StaleApprovals} customer request(s) awaiting approval beyond SLA.",
                issue.TenantId, issue.TenantName));
        }

        return Task.FromResult<IReadOnlyList<SystemAlert>>(alerts);
    }
}
