namespace EmsPortal.Api.Dashboard;

/// <summary>
/// Role-based default dashboard widget orders. Returned by the layout endpoint when a user has not
/// saved a personalised layout. Widget keys are the contract shared with the frontend.
/// </summary>
public static class DashboardDefaultLayouts
{
    /// <summary>Widgets every authenticated user sees. No generic (non-customer/non-user) widgets remain.</summary>
    public static readonly string[] Common = Array.Empty<string>();

    /// <summary>The customer onboarding charts/reports (shared by the Customer and Tenant Admin tiers).</summary>
    private static readonly string[] CustomerWidgets =
    {
        "customerKpiCards", "customerFunnel", "customerAgeing",
        "customerActivityFeed", "customerSubmissionTrend",
    };

    /// <summary>Customer-workflow users (data entry / review / approve) = the customer widgets.</summary>
    public static readonly string[] Customer = CustomerWidgets.ToArray();

    /// <summary>Tenant Admin = user widgets + customer widgets.</summary>
    public static readonly string[] TenantAdmin = new[] { "userSummary", "userRoleDistribution" }
        .Concat(CustomerWidgets)
        .ToArray();

    /// <summary>Super Admin = TenantAdmin + platform/cross-tenant widgets.</summary>
    public static readonly string[] SuperAdmin = TenantAdmin.Concat(new[]
    {
        "tenantKpiCards", "tenantHealthTable", "platformGrowthChart",
        "tenantOnboardingPanel", "systemAlertsPanel", "platformUserAnalytics", "crossTenantCustomerKpi",
        "customerIssuesTable", "crossTenantCustomerChart", "customerConversionFunnel",
    }).ToArray();

    /// <summary>The ordered default widget keys for a resolved dashboard role.</summary>
    public static IReadOnlyList<string> For(DashboardRole role) => role switch
    {
        DashboardRole.SuperAdmin => SuperAdmin,
        DashboardRole.TenantAdmin => TenantAdmin,
        DashboardRole.Customer => Customer,
        _ => Common,
    };

    /// <summary>
    /// Widgets hidden by default: everything except the customer-related charts/reports. New users land
    /// on a customer-focused dashboard and can switch the rest on from Customise. Roles without any
    /// customer widgets (e.g. Common/Operator) hide nothing, so their dashboard stays fully populated.
    /// </summary>
    public static IReadOnlyList<string> DefaultHiddenFor(DashboardRole role)
    {
        var order = For(role);
        var hasCustomerWidgets = order.Any(IsCustomerWidget);
        return hasCustomerWidgets
            ? order.Where(key => !IsCustomerWidget(key)).ToArray()
            : Array.Empty<string>();
    }

    /// <summary>True for the customer-related widget keys (the only widgets shown by default).</summary>
    private static bool IsCustomerWidget(string key)
        => key.Contains("customer", StringComparison.OrdinalIgnoreCase);
}

/// <summary>The dashboard layout tier a caller resolves to (independent of the RBAC role string).</summary>
public enum DashboardRole
{
    Common = 0,
    TenantAdmin = 1,
    SuperAdmin = 2,
    Customer = 3,
}
