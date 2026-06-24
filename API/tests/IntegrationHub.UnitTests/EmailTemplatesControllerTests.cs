using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using IntegrationHub.Api.Controllers;
using IntegrationHub.Api.Security;
using IntegrationHub.Application.Abstractions.Email;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Shared.Contracts;
using IntegrationHub.Shared.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace IntegrationHub.UnitTests;

public class EmailTemplatesControllerTests
{
    private readonly Mock<IEmailTemplateService> _templates = new();

    private EmailTemplatesController Create() => new(_templates.Object);

    private EmailTemplatesController CreateWithUser(string role, Guid? tenantId = null)
    {
        var controller = Create();
        var claims = new List<Claim> { new(ClaimTypeNames.Subject, Guid.NewGuid().ToString()), new(ClaimTypeNames.Role, role) };
        if (tenantId is { } t)
        {
            claims.Add(new Claim(ClaimTypeNames.ActiveTenantId, t.ToString()));
        }
        var identity = new ClaimsIdentity(claims, "test", ClaimTypeNames.Subject, ClaimTypeNames.Role);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
        };
        return controller;
    }

    private static EmailTemplateDescriptor Descriptor() =>
        new("Welcome", "Welcome", "desc", "Subject", "Body", false, new[] { "FullName" });

    [Theory]
    [InlineData(nameof(EmailTemplatesController.List), Permissions.UsersRead)]
    [InlineData(nameof(EmailTemplatesController.Get), Permissions.UsersRead)]
    [InlineData(nameof(EmailTemplatesController.Preview), Permissions.UsersRead)]
    [InlineData(nameof(EmailTemplatesController.Save), Permissions.EmailManage)]
    [InlineData(nameof(EmailTemplatesController.Reset), Permissions.EmailManage)]
    public void Endpoints_require_the_expected_permission(string methodName, string expectedPermission)
    {
        var method = typeof(EmailTemplatesController).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        method.Should().NotBeNull();
        var attribute = method!.GetCustomAttribute<RequirePermissionAttribute>();
        attribute.Should().NotBeNull($"{methodName} should be gated by [RequirePermission]");
        attribute!.Policy.Should().EndWith(expectedPermission);
    }

    [Fact]
    public async Task Save_scopes_a_tenant_admin_to_their_active_tenant()
    {
        var tenant = Guid.NewGuid();
        _templates.Setup(t => t.GetAsync(tenant, EmailTemplateKey.Welcome, It.IsAny<CancellationToken>())).ReturnsAsync(Descriptor());

        var controller = CreateWithUser(Roles.TenantAdmin, tenant);
        var result = await controller.Save("Welcome", tenantId: null, global: false,
            new Api.Models.EmailTemplates.SaveEmailTemplateRequest { Subject = "S", Body = "B" }, default);

        result.Should().BeOfType<OkObjectResult>();
        _templates.Verify(t => t.SaveAsync(tenant, EmailTemplateKey.Welcome, "S", "B", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Save_targets_the_platform_default_for_a_super_admin_with_global_flag()
    {
        _templates.Setup(t => t.GetAsync(null, EmailTemplateKey.Welcome, It.IsAny<CancellationToken>())).ReturnsAsync(Descriptor());

        var controller = CreateWithUser(Roles.SuperAdmin, Guid.NewGuid());
        var result = await controller.Save("Welcome", tenantId: null, global: true,
            new Api.Models.EmailTemplates.SaveEmailTemplateRequest { Subject = "S", Body = "B" }, default);

        result.Should().BeOfType<OkObjectResult>();
        _templates.Verify(t => t.SaveAsync(null, EmailTemplateKey.Welcome, "S", "B", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task List_honours_the_super_admin_tenant_override()
    {
        var target = Guid.NewGuid();
        _templates.Setup(t => t.ListAsync(target, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { Descriptor() });

        var controller = CreateWithUser(Roles.SuperAdmin, Guid.NewGuid());
        var result = await controller.List(tenantId: target, global: false, default);

        result.Should().BeOfType<OkObjectResult>();
        _templates.Verify(t => t.ListAsync(target, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Save_forbids_a_tenant_admin_from_editing_the_platform_default()
    {
        var controller = CreateWithUser(Roles.TenantAdmin, Guid.NewGuid());
        var result = await controller.Save("Welcome", tenantId: null, global: true,
            new Api.Models.EmailTemplates.SaveEmailTemplateRequest { Subject = "S", Body = "B" }, default);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _templates.Verify(t => t.SaveAsync(It.IsAny<Guid?>(), It.IsAny<EmailTemplateKey>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Get_returns_404_for_an_unknown_key()
    {
        var controller = CreateWithUser(Roles.TenantAdmin, Guid.NewGuid());
        var result = await controller.Get("NotARealKey", tenantId: null, global: false, default);
        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
