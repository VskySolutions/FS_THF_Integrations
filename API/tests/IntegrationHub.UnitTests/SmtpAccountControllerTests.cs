using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using IntegrationHub.Api.Controllers;
using IntegrationHub.Api.Models.SmtpAccounts;
using IntegrationHub.Api.Security;
using IntegrationHub.Application.Abstractions.Email;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Shared.Contracts;
using IntegrationHub.Shared.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace IntegrationHub.UnitTests;

public class SmtpAccountControllerTests
{
    private readonly Mock<ISmtpAccountService> _service = new();
    private readonly Mock<ISmtpAccountRepository> _accounts = new();
    private readonly Mock<ITenantRepository> _tenants = new();
    private readonly Mock<IUserRepository> _users = new();

    public SmtpAccountControllerTests()
        => _users.Setup(u => u.GetFullNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

    private SmtpAccountController Create() => new(_service.Object, _accounts.Object, _tenants.Object, _users.Object);

    private SmtpAccountController CreateWithUser(string role, Guid? tenantId = null)
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

    private static SmtpAccount Account(Guid tenantId, bool isActive = false) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        AccountName = "Primary",
        Host = "smtp.example.com",
        Port = 587,
        EncryptionType = SmtpEncryptionType.StartTls,
        AuthType = SmtpAuthType.Plain,
        Username = "user",
        EncryptedPassword = "enc:secret",
        FromName = "Acme",
        FromEmail = "noreply@acme.com",
        IsActive = isActive,
    };

    // ---- Permission gates (reflection over the action attributes) ----

    [Theory]
    [InlineData(nameof(SmtpAccountController.List), Permissions.UsersRead)]
    [InlineData(nameof(SmtpAccountController.Get), Permissions.UsersRead)]
    [InlineData(nameof(SmtpAccountController.Create), Permissions.EmailManage)]
    [InlineData(nameof(SmtpAccountController.Update), Permissions.EmailManage)]
    [InlineData(nameof(SmtpAccountController.Delete), Permissions.EmailManage)]
    [InlineData(nameof(SmtpAccountController.Activate), Permissions.EmailManage)]
    [InlineData(nameof(SmtpAccountController.Test), Permissions.EmailManage)]
    public void Endpoints_require_the_expected_permission(string methodName, string expectedPermission)
    {
        var method = typeof(SmtpAccountController).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        method.Should().NotBeNull();
        var attribute = method!.GetCustomAttribute<RequirePermissionAttribute>();
        attribute.Should().NotBeNull($"{methodName} should be gated by [RequirePermission]");
        attribute!.Policy.Should().EndWith(expectedPermission);
    }

    [Fact]
    public void Summary_response_never_exposes_a_password_field()
    {
        var passwordProp = typeof(SmtpAccountSummaryResponse).GetProperties()
            .FirstOrDefault(p => p.Name.Contains("password", StringComparison.OrdinalIgnoreCase));
        passwordProp.Should().BeNull("the password is write-only and must never be returned");
    }

    // ---- Tenant scoping ----

    [Fact]
    public async Task List_scopes_a_non_super_admin_to_their_active_tenant()
    {
        var tenant = Guid.NewGuid();
        _accounts.Setup(a => a.ListByTenantAsync(tenant, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Account(tenant) });

        var controller = CreateWithUser(Roles.TenantAdmin, tenant);
        var result = await controller.List(tenantId: null, status: null, default);

        result.Should().BeOfType<OkObjectResult>();
        _accounts.Verify(a => a.ListByTenantAsync(tenant, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task List_honours_the_super_admin_tenant_override()
    {
        var active = Guid.NewGuid();
        var target = Guid.NewGuid();
        _tenants.Setup(t => t.GetByIdAsync(target, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tenant { Id = target, Name = "Other", Status = TenantStatus.Active });
        _accounts.Setup(a => a.ListByTenantAsync(target, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SmtpAccount>());

        var controller = CreateWithUser(Roles.SuperAdmin, active);
        var result = await controller.List(tenantId: target, status: null, default);

        result.Should().BeOfType<OkObjectResult>();
        _accounts.Verify(a => a.ListByTenantAsync(target, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- Error mapping ----

    [Fact]
    public async Task Create_maps_duplicate_name_to_400_with_duplicate_code()
    {
        var tenant = Guid.NewGuid();
        _service.Setup(s => s.CreateAsync(It.IsAny<CreateSmtpAccountInput>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SmtpAccountException(SmtpAccountErrorCodes.DuplicateName, "duplicate"));

        var controller = CreateWithUser(Roles.TenantAdmin, tenant);
        var body = new CreateSmtpAccountRequest
        {
            AccountName = "Primary", Host = "smtp.example.com", Port = 587,
            EncryptionType = "StartTls", AuthType = "Plain", FromName = "Acme", FromEmail = "noreply@acme.com"
        };
        var result = await controller.Create(body, default);

        var error = result.Should().BeOfType<BadRequestObjectResult>().Subject.Value.Should().BeOfType<ApiErrorResponse>().Subject;
        error.Error!.Code.Should().Be(ApiErrorCodes.DuplicateIdentifier);
    }

    [Fact]
    public async Task Delete_maps_active_account_block_to_400()
    {
        var tenant = Guid.NewGuid();
        _service.Setup(s => s.DeleteAsync(It.IsAny<Guid>(), tenant, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SmtpAccountException(SmtpAccountErrorCodes.ActiveAccountDelete, "cannot delete active"));

        var controller = CreateWithUser(Roles.TenantAdmin, tenant);
        var result = await controller.Delete(Guid.NewGuid(), tenantId: null, default);

        var error = result.Should().BeOfType<BadRequestObjectResult>().Subject.Value.Should().BeOfType<ApiErrorResponse>().Subject;
        error.Error!.Code.Should().Be(ApiErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Get_returns_404_when_the_account_is_not_found()
    {
        var tenant = Guid.NewGuid();
        _accounts.Setup(a => a.GetByIdAsync(It.IsAny<Guid>(), tenant, It.IsAny<CancellationToken>())).ReturnsAsync((SmtpAccount?)null);

        var controller = CreateWithUser(Roles.TenantAdmin, tenant);
        var result = await controller.Get(Guid.NewGuid(), tenantId: null, default);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
