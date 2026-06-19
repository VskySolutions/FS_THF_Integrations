namespace IntegrationHub.Api.Dashboard;

/// <summary>
/// Role-based default dashboard widget orders. Returned by the layout endpoint when a user has not
/// saved a personalised layout. Widget keys are the contract shared with the frontend.
/// </summary>
public static class DashboardDefaultLayouts
{
    /// <summary>Widgets every authenticated user sees (jobs + health).</summary>
    public static readonly string[] Common =
    {
        "jobKpiCards", "jobSuccessGauge", "jobVolumeTrend", "flowBreakdown",
        "systemHealth", "failedJobsPanel", "retryQueue",
    };

    /// <summary>Tenant Admin = Common + customer/user widgets.</summary>
    public static readonly string[] TenantAdmin = Common.Concat(new[]
    {
        "userSummary", "userRoleDistribution", "customerKpiCards", "customerFunnel",
        "customerAgeing", "customerSyncHealth", "customerActivityFeed", "customerSubmissionTrend",
    }).ToArray();

    /// <summary>Super Admin = TenantAdmin + platform/cross-tenant widgets.</summary>
    public static readonly string[] SuperAdmin = TenantAdmin.Concat(new[]
    {
        "tenantKpiCards", "crossTenantJobChart", "tenantHealthTable", "platformGrowthChart",
        "tenantOnboardingPanel", "systemAlertsPanel", "platformUserAnalytics", "crossTenantCustomerKpi",
        "customerIssuesTable", "crossTenantCustomerChart", "customerConversionFunnel",
    }).ToArray();

    /// <summary>The ordered default widget keys for a resolved dashboard role.</summary>
    public static IReadOnlyList<string> For(DashboardRole role) => role switch
    {
        DashboardRole.SuperAdmin => SuperAdmin,
        DashboardRole.TenantAdmin => TenantAdmin,
        _ => Common,
    };
}

/// <summary>The dashboard layout tier a caller resolves to (independent of the RBAC role string).</summary>
public enum DashboardRole
{
    Common = 0,
    TenantAdmin = 1,
    SuperAdmin = 2,
}
