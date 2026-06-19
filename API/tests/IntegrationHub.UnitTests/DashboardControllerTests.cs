using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using IntegrationHub.Api.Controllers;
using IntegrationHub.Api.Dashboard;
using IntegrationHub.Api.Models.Dashboard;
using IntegrationHub.Api.Security;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Shared.Contracts;
using IntegrationHub.Shared.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace IntegrationHub.UnitTests;

// WO-77: DashboardController scoping, platform gating, layout persistence + permission attributes.
public class DashboardControllerTests
{
    private readonly Mock<IDashboardQueryService> _query = new();
    private readonly Mock<IDashboardCacheService> _cache = new();
    private readonly Mock<IDashboardLayoutRepository> _layouts = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private DashboardController Create() => new(_query.Object, _cache.Object, _layouts.Object, _unitOfWork.Object);

    /// <summary>Builds a controller with the given identity claims (subject + role + optional tenant + explicit permissions).</summary>
    private DashboardController CreateWithUser(Guid userId, string role, Guid? tenantId = null, params string[] permissions)
    {
        var controller = Create();
        var claims = new List<Claim>
        {
            new(ClaimTypeNames.Subject, userId.ToString()),
            new(ClaimTypeNames.Role, role),
        };
        if (tenantId is { } t)
        {
            claims.Add(new Claim(ClaimTypeNames.ActiveTenantId, t.ToString()));
        }
        foreach (var permission in permissions)
        {
            claims.Add(new Claim(ClaimTypeNames.Permission, permission));
        }

        var identity = new ClaimsIdentity(claims, "test", ClaimTypeNames.Subject, ClaimTypeNames.Role);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
        };
        return controller;
    }

    private static JobDashboardDto EmptyJobs()
        => new(new JobKpisDto(0, 0, 0, 0, 0, 0, 0, 0), 0, Array.Empty<DailyJobCount>(),
            Array.Empty<FlowJobCount>(), Array.Empty<FailedJobSummary>(), 0, null);

    private static CustomerDashboardDto EmptyCustomers()
        => new(new CustomerKpisDto(0, 0, 0, 0, 0, 0, 0), Array.Empty<StageCount>(), Array.Empty<AgeingItem>(),
            new SyncHealthDto(0, 0, 0, 0, 0, Array.Empty<SyncTimelinePoint>(), Array.Empty<SyncFailureItem>()),
            Array.Empty<ActivityEntry>(), Array.Empty<SubmitterCount>(), Array.Empty<SubmissionTrendPoint>());

    private static UserDashboardDto EmptyUsers()
        => new(new UserKpisDto(0, 0, 0, 0, 0, 0), Array.Empty<RoleCount>(), Array.Empty<ActivityEntry>());

    // ---- Tenant scoping ----

    [Fact]
    public async Task Jobs_for_normal_user_scopes_to_active_tenant_ignoring_requested_tenant()
    {
        var activeTenant = Guid.NewGuid();
        Guid? scopePassed = null;
        _query.Setup(q => q.GetJobsAsync(It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid?, string, CancellationToken>((t, _, _) => scopePassed = t)
            .ReturnsAsync(EmptyJobs());

        var controller = CreateWithUser(Guid.NewGuid(), Roles.Operator, activeTenant);
        await controller.Jobs("7d", tenantId: Guid.NewGuid(), default);

        scopePassed.Should().Be(activeTenant);
    }

    [Fact]
    public async Task Jobs_for_super_admin_forwards_requested_tenant()
    {
        var target = Guid.NewGuid();
        Guid? scopePassed = null;
        _query.Setup(q => q.GetJobsAsync(It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid?, string, CancellationToken>((t, _, _) => scopePassed = t)
            .ReturnsAsync(EmptyJobs());

        var controller = CreateWithUser(Guid.NewGuid(), Roles.SuperAdmin, Guid.NewGuid());
        await controller.Jobs("7d", tenantId: target, default);

        scopePassed.Should().Be(target);
    }

    [Fact]
    public async Task Customers_for_normal_user_scopes_to_active_tenant()
    {
        var activeTenant = Guid.NewGuid();
        Guid? scopePassed = null;
        _query.Setup(q => q.GetCustomersAsync(It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid?, string, CancellationToken>((t, _, _) => scopePassed = t)
            .ReturnsAsync(EmptyCustomers());

        var controller = CreateWithUser(Guid.NewGuid(), Roles.TenantAdmin, activeTenant);
        await controller.Customers("7d", tenantId: Guid.NewGuid(), default);

        scopePassed.Should().Be(activeTenant);
    }

    [Fact]
    public async Task Users_for_super_admin_forwards_requested_tenant()
    {
        var target = Guid.NewGuid();
        Guid? scopePassed = null;
        _query.Setup(q => q.GetUsersAsync(It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid?, string, CancellationToken>((t, _, _) => scopePassed = t)
            .ReturnsAsync(EmptyUsers());

        var controller = CreateWithUser(Guid.NewGuid(), Roles.SuperAdmin);
        await controller.Users("7d", tenantId: target, default);

        scopePassed.Should().Be(target);
    }

    // ---- Platform gating ----

    [Fact]
    public async Task Platform_for_super_admin_returns_200()
    {
        var dto = new PlatformDashboardDto(
            new TenantKpisDto(0, 0, 0, 0, 0, 0, 0), Array.Empty<CrossTenantJobCount>(), Array.Empty<TenantHealthRow>(),
            Array.Empty<GrowthPoint>(), Array.Empty<OnboardingRow>(), Array.Empty<SystemAlert>(),
            new PlatformUserAnalyticsDto(0, 0, 0, 0, 0, Array.Empty<GrowthPoint>(), Array.Empty<TenantCount>(), Array.Empty<ActivityEntry>()),
            new PlatformCustomerDto(0, 0, 0, 0, 0, 0, Array.Empty<TenantCount>(), Array.Empty<SyncTimelinePoint>(), Array.Empty<StageCount>(), Array.Empty<CustomerIssueRow>()));
        _cache.Setup(c => c.GetOrAddAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Func<Task<PlatformDashboardDto>>>()))
            .ReturnsAsync(dto);

        var controller = CreateWithUser(Guid.NewGuid(), Roles.SuperAdmin);
        var result = await controller.Platform("7d", default);

        result.Should().BeOfType<OkObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Platform_for_non_super_admin_is_forbidden()
    {
        var controller = CreateWithUser(Guid.NewGuid(), Roles.TenantAdmin, Guid.NewGuid());
        var result = await controller.Platform("7d", default);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _cache.Verify(c => c.GetOrAddAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Func<Task<PlatformDashboardDto>>>()), Times.Never);
    }

    // ---- Layout: GET ----

    [Fact]
    public async Task GetLayout_returns_saved_layout_when_present()
    {
        var userId = Guid.NewGuid();
        var saved = new DashboardLayout
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            WidgetOrderJson = "[\"widgetA\",\"widgetB\"]",
            HiddenWidgetsJson = "[\"widgetC\"]",
            CollapsedWidgetsJson = "[]",
        };
        _layouts.Setup(l => l.GetByUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(saved);

        var controller = CreateWithUser(userId, Roles.Operator, Guid.NewGuid());
        var result = await controller.GetLayout(default);

        var data = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<ApiResponse<DashboardLayoutResponse>>().Subject.Data!;
        data.WidgetOrder.Should().Equal("widgetA", "widgetB");
        data.HiddenWidgets.Should().Equal("widgetC");
    }

    [Fact]
    public async Task GetLayout_returns_role_default_when_none_saved_for_super_admin()
    {
        var userId = Guid.NewGuid();
        _layouts.Setup(l => l.GetByUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((DashboardLayout?)null);

        var controller = CreateWithUser(userId, Roles.SuperAdmin);
        var result = await controller.GetLayout(default);

        var data = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<ApiResponse<DashboardLayoutResponse>>().Subject.Data!;
        data.WidgetOrder.Should().Equal(DashboardDefaultLayouts.For(DashboardRole.SuperAdmin));
    }

    [Fact]
    public async Task GetLayout_returns_common_default_for_plain_user_when_none_saved()
    {
        var userId = Guid.NewGuid();
        _layouts.Setup(l => l.GetByUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((DashboardLayout?)null);

        // Operator without users.read + tenants.read resolves to the Common tier.
        var controller = CreateWithUser(userId, Roles.Operator, Guid.NewGuid());
        var result = await controller.GetLayout(default);

        var data = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<ApiResponse<DashboardLayoutResponse>>().Subject.Data!;
        data.WidgetOrder.Should().Equal(DashboardDefaultLayouts.For(DashboardRole.Common));
    }

    // ---- Layout: PUT ----

    [Fact]
    public async Task SaveLayout_persists_widget_order_and_saves_changes()
    {
        var userId = Guid.NewGuid();
        DashboardLayout? captured = null;
        _layouts.Setup(l => l.UpsertAsync(It.IsAny<DashboardLayout>(), It.IsAny<CancellationToken>()))
            .Callback<DashboardLayout, CancellationToken>((l, _) => captured = l);

        var controller = CreateWithUser(userId, Roles.Operator, Guid.NewGuid());
        var order = new List<string> { "widgetX", "widgetY", "widgetZ" };
        var result = await controller.SaveLayout(
            new DashboardLayoutRequest { WidgetOrder = order }, default);

        result.Should().BeOfType<OkObjectResult>();
        captured.Should().NotBeNull();
        captured!.UserId.Should().Be(userId);
        captured.WidgetOrder.Should().Equal(order);
        _layouts.Verify(l => l.UpsertAsync(It.IsAny<DashboardLayout>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- Permission gating attributes ----

    [Theory]
    [InlineData(nameof(DashboardController.Jobs), Permissions.JobsRead)]
    [InlineData(nameof(DashboardController.Health), Permissions.HealthRead)]
    [InlineData(nameof(DashboardController.Customers), Permissions.CustomersReview)]
    [InlineData(nameof(DashboardController.Users), Permissions.UsersRead)]
    public void Sections_require_expected_permission(string methodName, string expectedPermission)
    {
        var method = typeof(DashboardController).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        method.Should().NotBeNull($"{methodName} should exist");

        var attribute = method!.GetCustomAttribute<RequirePermissionAttribute>();
        attribute.Should().NotBeNull($"{methodName} should be gated by [RequirePermission]");
        attribute!.Policy.Should().EndWith(expectedPermission);
    }
}
