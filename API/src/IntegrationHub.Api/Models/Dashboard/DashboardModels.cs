namespace IntegrationHub.Api.Models.Dashboard;

// ---- Jobs ----

public sealed record JobKpisDto(
    int Total, int Completed, int Failed, int Pending,
    double TotalTrendPct, double CompletedTrendPct, double FailedTrendPct, double PendingTrendPct);

/// <summary>Per-day job counts. <c>Date</c> is "yyyy-MM-dd".</summary>
public sealed record DailyJobCount(string Date, int Completed, int Failed, int Pending);

public sealed record FlowJobCount(string Flow, string Label, int Completed, int Failed, int Pending);

public sealed record FailedJobSummary(Guid JobId, string InterfaceName, string FlowLabel, string? ErrorMessage, DateTime? FailedAtUtc);

public sealed record JobDashboardDto(
    JobKpisDto Kpis,
    double SuccessRate,
    IReadOnlyList<DailyJobCount> VolumeChart,
    IReadOnlyList<FlowJobCount> FlowBreakdown,
    IReadOnlyList<FailedJobSummary> FailedJobs,
    int RetryQueueCount,
    DateTime? RetryQueueNextRunUtc);

// ---- Health ----

public sealed record HealthComponentDto(string Name, string Status, string? Description);

public sealed record HealthDashboardDto(string Status, IReadOnlyList<HealthComponentDto> Components, bool AllOperational);

// ---- Customers ----

/// <summary>PendingAction = Submitted + UnderReview + PendingApproval + PartiallyApproved + Returned.</summary>
public sealed record CustomerKpisDto(
    int Total, int Synced, int PendingAction, int SyncFailed, int Rejected,
    double TotalTrendPct, double SyncedTrendPct);

public sealed record StageCount(string Stage, int Count);

public sealed record AgeingItem(Guid RequestId, string? CustomerRequestNumber, string CompanyName, string Status, int DaysInStatus, bool SlaBreach);

public sealed record SyncTimelinePoint(string Date, int Synced, int Failed);

public sealed record SyncFailureItem(Guid RequestId, string? CustomerRequestNumber, string CompanyName, string? ErrorMessage, DateTime? FailedAtUtc);

public sealed record SyncHealthDto(
    int TotalSynced, int SyncedThisMonth, double SuccessRate, int InProgress, int FailedAwaitingRetry,
    IReadOnlyList<SyncTimelinePoint> Timeline, IReadOnlyList<SyncFailureItem> RecentFailures);

public sealed record ActivityEntry(
    Guid Id, string Action, string? Actor, DateTime TimestampUtc,
    Guid? CustomerRequestId, string? CustomerRequestNumber, string? Notes);

public sealed record SubmitterCount(Guid? SubmitterId, string SubmitterName, int Count);

public sealed record SubmissionTrendPoint(string WeekStart, int Submitted, int Approved);

public sealed record CustomerDashboardDto(
    CustomerKpisDto Kpis,
    IReadOnlyList<StageCount> Funnel,
    IReadOnlyList<AgeingItem> Ageing,
    SyncHealthDto SyncHealth,
    IReadOnlyList<ActivityEntry> ActivityFeed,
    IReadOnlyList<SubmitterCount> TopSubmitters,
    IReadOnlyList<SubmissionTrendPoint> SubmissionTrend);

// ---- Users ----

public sealed record UserKpisDto(
    int Total, int LoggedInToday, int ActiveThisWeek, int Inactive30Days, int PendingFirstLogin, int NewThisMonth);

public sealed record RoleCount(string Role, int Count);

public sealed record UserDashboardDto(
    UserKpisDto Kpis,
    IReadOnlyList<RoleCount> RoleDistribution,
    IReadOnlyList<ActivityEntry> ActivityFeed);

// ---- Platform (Super Admin) ----

public sealed record TenantKpisDto(
    int ActiveTenants, int InactiveTenants, int ArchivedTenants, int TotalUsers, int JobsToday,
    double PlatformSuccessRate, int PendingCustomerApprovals);

public sealed record CrossTenantJobCount(Guid TenantId, string TenantName, int Completed, int Failed, int Pending);

public sealed record TenantHealthRow(
    Guid TenantId, string TenantName, bool ConcurConfigured, bool MaconomyConfigured,
    DateTime? LastJobRunUtc, double SuccessRate, int PendingCustomers, int ActiveUsers);

public sealed record GrowthPoint(string Date, int Tenants, int Users);

public sealed record OnboardingRow(Guid TenantId, string TenantName, bool MissingCredentials, bool MissingUsers, bool MissingSchedules);

public sealed record SystemAlert(string Id, string Type, string Severity, string Message, Guid? TenantId, string? TenantName);

public sealed record TenantCount(string TenantName, int Count);

public sealed record PlatformUserAnalyticsDto(
    int TotalActive, int LoggedInToday, int PendingFirstLogin, int NoRole, int NewThisMonth,
    IReadOnlyList<GrowthPoint> Growth, IReadOnlyList<TenantCount> ByTenant, IReadOnlyList<ActivityEntry> ActivityFeed);

public sealed record CustomerIssueRow(Guid TenantId, string TenantName, int StaleApprovals, int SyncFailures, int RepeatedReturns);

public sealed record PlatformCustomerDto(
    int Total, int Synced, int PendingApproval, int SyncFailed, int Rejected, double TotalTrendPct,
    IReadOnlyList<TenantCount> ByTenant, IReadOnlyList<SyncTimelinePoint> SyncTimeline,
    IReadOnlyList<StageCount> Funnel, IReadOnlyList<CustomerIssueRow> Issues);

public sealed record PlatformDashboardDto(
    TenantKpisDto TenantKpis,
    IReadOnlyList<CrossTenantJobCount> CrossTenantJobs,
    IReadOnlyList<TenantHealthRow> TenantHealth,
    IReadOnlyList<GrowthPoint> Growth,
    IReadOnlyList<OnboardingRow> Onboarding,
    IReadOnlyList<SystemAlert> SystemAlerts,
    PlatformUserAnalyticsDto UserAnalytics,
    PlatformCustomerDto Customer);

// ---- Layout ----

public sealed record DashboardLayoutResponse(
    IReadOnlyList<string> WidgetOrder,
    IReadOnlyList<string> HiddenWidgets,
    IReadOnlyList<string> CollapsedWidgets);

public sealed class DashboardLayoutRequest
{
    public List<string> WidgetOrder { get; set; } = new();
    public List<string> HiddenWidgets { get; set; } = new();
    public List<string> CollapsedWidgets { get; set; } = new();
}
