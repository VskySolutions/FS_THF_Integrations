namespace EmsPortal.Api.Models.Dashboard;

// ---- Shared ----

public sealed record ActivityEntry(
    Guid Id, string Action, string? Actor, DateTime TimestampUtc, string? Notes);

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
    int ActiveTenants, int InactiveTenants, int ArchivedTenants, int TotalUsers);

public sealed record TenantHealthRow(
    Guid TenantId, string TenantName, int ActiveUsers);

public sealed record GrowthPoint(string Date, int Tenants, int Users);

public sealed record OnboardingRow(Guid TenantId, string TenantName, bool MissingUsers);

public sealed record SystemAlert(string Id, string Type, string Severity, string Message, Guid? TenantId, string? TenantName);

public sealed record TenantCount(string TenantName, int Count);

public sealed record PlatformUserAnalyticsDto(
    int TotalActive, int LoggedInToday, int PendingFirstLogin, int NoRole, int NewThisMonth,
    IReadOnlyList<GrowthPoint> Growth, IReadOnlyList<TenantCount> ByTenant, IReadOnlyList<ActivityEntry> ActivityFeed);

public sealed record PlatformDashboardDto(
    TenantKpisDto TenantKpis,
    IReadOnlyList<TenantHealthRow> TenantHealth,
    IReadOnlyList<GrowthPoint> Growth,
    IReadOnlyList<OnboardingRow> Onboarding,
    IReadOnlyList<SystemAlert> SystemAlerts,
    PlatformUserAnalyticsDto UserAnalytics);

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
