using FluentAssertions;
using IntegrationHub.Api.Dashboard;

namespace IntegrationHub.UnitTests;

// WO-77: role-based default widget layouts.
public class DashboardDefaultLayoutsTests
{
    [Fact]
    public void Common_role_returns_common_widgets()
    {
        DashboardDefaultLayouts.For(DashboardRole.Common).Should().Equal(DashboardDefaultLayouts.Common);
    }

    [Fact]
    public void TenantAdmin_extends_common_with_customer_and_user_widgets()
    {
        var layout = DashboardDefaultLayouts.For(DashboardRole.TenantAdmin);

        layout.Should().StartWith(DashboardDefaultLayouts.Common);
        layout.Should().Contain("customerKpiCards");
        layout.Should().Contain("userSummary");
        layout.Count.Should().BeGreaterThan(DashboardDefaultLayouts.Common.Length);
    }

    [Fact]
    public void SuperAdmin_extends_tenant_admin_with_platform_widgets()
    {
        var layout = DashboardDefaultLayouts.For(DashboardRole.SuperAdmin);

        layout.Should().StartWith(DashboardDefaultLayouts.TenantAdmin);
        layout.Should().Contain("tenantKpiCards");
        layout.Should().Contain("crossTenantJobChart");
        layout.Count.Should().BeGreaterThan(DashboardDefaultLayouts.TenantAdmin.Length);
    }

    [Fact]
    public void Customer_role_is_common_plus_customer_widgets_only()
    {
        var layout = DashboardDefaultLayouts.For(DashboardRole.Customer);

        layout.Should().StartWith(DashboardDefaultLayouts.Common);
        layout.Should().Contain("customerKpiCards");
        // No user/platform widgets for a customer-workflow user.
        layout.Should().NotContain("userSummary");
        layout.Should().NotContain("tenantKpiCards");
        layout.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Customer_default_hidden_leaves_only_customer_widgets_visible()
    {
        var order = DashboardDefaultLayouts.For(DashboardRole.Customer);
        var hidden = DashboardDefaultLayouts.DefaultHiddenFor(DashboardRole.Customer);

        // Every non-customer (job/health) widget is hidden; the customer charts stay visible.
        hidden.Should().Contain("jobKpiCards");
        hidden.Should().NotContain("customerKpiCards");
        order.Except(hidden).Should().OnlyContain(key => key.Contains("customer", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Layouts_have_no_duplicate_widget_keys()
    {
        DashboardDefaultLayouts.SuperAdmin.Should().OnlyHaveUniqueItems();
    }
}
