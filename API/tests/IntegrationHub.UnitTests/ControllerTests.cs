using System.Security.Claims;
using FluentAssertions;
using IntegrationHub.Api.Controllers;
using IntegrationHub.Api.Security;
using IntegrationHub.Api.Models.Auth;
using IntegrationHub.Api.Models.Tenants;
using IntegrationHub.Api.Models.Users;
using IntegrationHub.Application.Abstractions.Auditing;
using IntegrationHub.Application.Abstractions.Connectors.Concur;
using IntegrationHub.Application.Abstractions.Connectors.Maconomy;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Security;
using IntegrationHub.Application.Abstractions.Tenancy;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Shared.Contracts;
using IntegrationHub.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using AuthenticationOptions = IntegrationHub.Shared.Configuration.AuthenticationOptions;

namespace IntegrationHub.UnitTests;

internal static class ControllerTestExtensions
{
    public static T WithUser<T>(this T controller, Guid userId, string role = "SuperAdmin", Guid? tenantId = null) where T : ControllerBase
    {
        var claims = new List<Claim> { new(ClaimTypeNames.Subject, userId.ToString()), new(ClaimTypeNames.Role, role) };
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
}

// WO-43: AuthController login behavior.
public class AuthControllerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IRefreshTokenRepository> _refresh = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IJwtTokenService> _jwt = new();
    private readonly Mock<ITenantRepository> _tenants = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private AuthController Create() => new(
        _users.Object, _refresh.Object, _hasher.Object, _jwt.Object, _tenants.Object, _unitOfWork.Object,
        Options.Create(new AuthenticationOptions()));

    [Fact]
    public async Task Login_with_valid_credentials_returns_token_with_must_change_flag()
    {
        var user = TestData.User(mustChangePassword: true);
        _users.Setup(u => u.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), user.PasswordHash, user.Salt)).Returns(true);
        _jwt.Setup(j => j.CreateAccessToken(user, It.IsAny<Guid>())).Returns(new AccessToken("tok", 3600));

        var result = await Create().Login(new LoginRequest { Email = user.Email, Password = "x" }, default);

        var envelope = result.Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeOfType<ApiResponse<LoginTokenResponse>>().Subject;
        envelope.Success.Should().BeTrue();
        envelope.Data!.MustChangePassword.Should().BeTrue();
        envelope.Data.AccessToken.Should().Be("tok");
    }

    [Fact]
    public async Task Login_with_invalid_password_is_unauthorized()
    {
        var user = TestData.User();
        _users.Setup(u => u.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var result = await Create().Login(new LoginRequest { Email = user.Email, Password = "bad" }, default);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_with_inactive_account_is_unauthorized()
    {
        var user = TestData.User(isActive: false);
        _users.Setup(u => u.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var result = await Create().Login(new LoginRequest { Email = user.Email, Password = "x" }, default);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Switch_to_unassigned_tenant_is_forbidden()
    {
        var user = TestData.User();
        user.TenantRoles.Add(TestData.Assignment(user.Id, Guid.NewGuid(), UserRole.Operator));
        _users.Setup(u => u.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var controller = Create().WithUser(user.Id, Roles.Operator);
        var result = await controller.SwitchTenant(new SwitchTenantRequest { TenantId = Guid.NewGuid() }, default);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }
}

// WO-43 / WO-48: UsersController authorization logic.
public class UsersControllerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IAuditTrailService> _audit = new();
    private readonly Mock<IRoleRepository> _roles = new();
    private readonly Mock<IPersonRepository> _persons = new();

    private UsersController Create() => new(_users.Object, _hasher.Object, _refreshTokens.Object, _unitOfWork.Object, _audit.Object, _roles.Object, _persons.Object);

    [Fact]
    public async Task Super_admin_creates_user_returns_201_with_temp_password()
    {
        _users.Setup(u => u.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _hasher.Setup(h => h.GenerateTemporaryPassword()).Returns("Temp123!");
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns(("h", "s"));

        var controller = Create().WithUser(Guid.NewGuid(), Roles.SuperAdmin);
        var result = await controller.Create(new CreateUserRequest { Email = "n@t.com", DisplayName = "N", Role = "Operator", TenantId = Guid.NewGuid() }, default);

        var obj = result.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(StatusCodes.Status201Created);
        obj.Value.Should().BeOfType<ApiResponse<CreateUserResponse>>().Which.Data!.TemporaryPassword.Should().Be("Temp123!");
    }

    [Fact]
    public async Task Duplicate_email_is_conflict()
    {
        _users.Setup(u => u.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var controller = Create().WithUser(Guid.NewGuid(), Roles.SuperAdmin);
        var result = await controller.Create(new CreateUserRequest { Email = "dup@t.com", DisplayName = "N", Role = "Operator", TenantId = Guid.NewGuid() }, default);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task Create_with_system_roleId_links_roleId_and_maps_legacy_enum()
    {
        var tenantId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        UserTenantRole? captured = null;
        _users.Setup(u => u.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _users.Setup(u => u.AddAssignmentAsync(It.IsAny<UserTenantRole>(), It.IsAny<CancellationToken>()))
            .Callback<UserTenantRole, CancellationToken>((a, _) => captured = a);
        _hasher.Setup(h => h.GenerateTemporaryPassword()).Returns("Temp123!");
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns(("h", "s"));
        _roles.Setup(r => r.GetByIdAsync(roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Role { Id = roleId, Name = "Operator", IsSystem = true });

        var controller = Create().WithUser(Guid.NewGuid(), Roles.SuperAdmin);
        var result = await controller.Create(
            new CreateUserRequest { Email = "n@t.com", DisplayName = "N", RoleId = roleId, TenantId = tenantId }, default);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status201Created);
        captured.Should().NotBeNull();
        captured!.RoleId.Should().Be(roleId);
        captured.Role.Should().Be(UserRole.Operator);
    }

    [Fact]
    public async Task Create_with_custom_roleId_not_available_to_tenant_is_bad_request()
    {
        var tenantId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        _users.Setup(u => u.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _hasher.Setup(h => h.GenerateTemporaryPassword()).Returns("Temp123!");
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns(("h", "s"));
        _roles.Setup(r => r.GetByIdAsync(roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Role { Id = roleId, Name = "Auditor", IsSystem = false });
        _roles.Setup(r => r.GetTenantRoleAsync(tenantId, roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantRole?)null);

        var controller = Create().WithUser(Guid.NewGuid(), Roles.SuperAdmin);
        var result = await controller.Create(
            new CreateUserRequest { Email = "n@t.com", DisplayName = "N", RoleId = roleId, TenantId = tenantId }, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Tenant_admin_cannot_create_super_admin()
    {
        var controller = Create().WithUser(Guid.NewGuid(), Roles.TenantAdmin, Guid.NewGuid());
        var result = await controller.Create(new CreateUserRequest { Email = "n@t.com", DisplayName = "N", Role = "SuperAdmin" }, default);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Tenant_admin_cannot_deactivate_super_admin()
    {
        var target = TestData.User();
        var tenantId = Guid.NewGuid();
        target.TenantRoles.Add(TestData.Assignment(target.Id, tenantId, UserRole.SuperAdmin));
        _users.Setup(u => u.GetByIdAsync(target.Id, It.IsAny<CancellationToken>())).ReturnsAsync(target);

        var controller = Create().WithUser(Guid.NewGuid(), Roles.TenantAdmin, tenantId);
        var result = await controller.SetStatus(target.Id, new UpdateUserStatusRequest { IsActive = false }, default);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Reset_password_returns_temp_password_and_forces_change()
    {
        var target = TestData.User();
        var tenantId = Guid.NewGuid();
        target.TenantRoles.Add(TestData.Assignment(target.Id, tenantId, UserRole.Operator));
        _users.Setup(u => u.GetByIdAsync(target.Id, It.IsAny<CancellationToken>())).ReturnsAsync(target);
        _hasher.Setup(h => h.GenerateTemporaryPassword()).Returns("Temp123!");
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns(("h", "s"));

        var controller = Create().WithUser(Guid.NewGuid(), Roles.SuperAdmin);
        var result = await controller.ResetPassword(target.Id, default);

        var obj = result.Should().BeOfType<OkObjectResult>().Subject;
        obj.Value.Should().BeOfType<ApiResponse<ResetPasswordResponse>>().Which.Data!.TemporaryPassword.Should().Be("Temp123!");
        target.MustChangePassword.Should().BeTrue();
        _refreshTokens.Verify(r => r.RevokeAllForUserAsync(target.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Tenant_admin_cannot_reset_super_admin_password()
    {
        var target = TestData.User();
        var tenantId = Guid.NewGuid();
        target.TenantRoles.Add(TestData.Assignment(target.Id, tenantId, UserRole.SuperAdmin));
        _users.Setup(u => u.GetByIdAsync(target.Id, It.IsAny<CancellationToken>())).ReturnsAsync(target);

        var controller = Create().WithUser(Guid.NewGuid(), Roles.TenantAdmin, tenantId);
        var result = await controller.ResetPassword(target.Id, default);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Tenant_admin_assigns_role_within_active_tenant()
    {
        var tenantId = Guid.NewGuid();
        var target = TestData.User();
        target.TenantRoles.Add(TestData.Assignment(target.Id, tenantId, UserRole.Operator));
        _users.Setup(u => u.GetByIdAsync(target.Id, It.IsAny<CancellationToken>())).ReturnsAsync(target);
        _users.Setup(u => u.GetAssignmentAsync(target.Id, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target.TenantRoles.First());

        var controller = Create().WithUser(Guid.NewGuid(), Roles.TenantAdmin, tenantId);
        var result = await controller.AssignTenantRole(target.Id,
            new AssignTenantRoleRequest { TenantId = tenantId, Role = "Operator" }, default);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Tenant_admin_cannot_assign_role_in_another_tenant()
    {
        var target = TestData.User();
        _users.Setup(u => u.GetByIdAsync(target.Id, It.IsAny<CancellationToken>())).ReturnsAsync(target);

        var controller = Create().WithUser(Guid.NewGuid(), Roles.TenantAdmin, Guid.NewGuid());
        var result = await controller.AssignTenantRole(target.Id,
            new AssignTenantRoleRequest { TenantId = Guid.NewGuid(), Role = "Operator" }, default);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Tenant_admin_cannot_assign_super_admin_role()
    {
        var tenantId = Guid.NewGuid();
        var target = TestData.User();
        _users.Setup(u => u.GetByIdAsync(target.Id, It.IsAny<CancellationToken>())).ReturnsAsync(target);

        var controller = Create().WithUser(Guid.NewGuid(), Roles.TenantAdmin, tenantId);
        var result = await controller.AssignTenantRole(target.Id,
            new AssignTenantRoleRequest { TenantId = tenantId, Role = "SuperAdmin" }, default);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Tenant_admin_cannot_remove_assignment_in_another_tenant()
    {
        var controller = Create().WithUser(Guid.NewGuid(), Roles.TenantAdmin, Guid.NewGuid());
        var result = await controller.RemoveTenantRole(Guid.NewGuid(), Guid.NewGuid(), default);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Tenant_admin_list_is_scoped_to_their_tenant()
    {
        var tenantId = Guid.NewGuid();
        _users.Setup(u => u.ListAsync(It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<User>(), 0));
        _users.Setup(u => u.GetFullNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        var controller = Create().WithUser(Guid.NewGuid(), Roles.TenantAdmin, tenantId);
        await controller.List(1, 20, default);

        _users.Verify(u => u.ListAsync(tenantId, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }
}

// WO-60 RBAC Phase 3: permission-based authorization handler.
public class PermissionAuthorizationHandlerTests
{
    private static AuthorizationHandlerContext Context(params Claim[] claims)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test", ClaimTypeNames.Subject, ClaimTypeNames.Role));
        var requirement = new PermissionRequirement(Permissions.TenantsWrite);
        return new AuthorizationHandlerContext(new[] { requirement }, principal, resource: null);
    }

    [Fact]
    public async Task Grants_when_explicit_permission_claim_present()
    {
        var context = Context(new Claim(ClaimTypeNames.Permission, Permissions.TenantsWrite));
        await new PermissionAuthorizationHandler().HandleAsync(context);
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Grants_via_role_claim_fallback_for_system_role()
    {
        // Super Admin's seeded set includes tenants.write — no explicit permission claim needed.
        var context = Context(new Claim(ClaimTypeNames.Role, Roles.SuperAdmin));
        await new PermissionAuthorizationHandler().HandleAsync(context);
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Denies_when_role_lacks_permission_and_no_claim()
    {
        // Operator's seeded set does not include tenants.write.
        var context = Context(new Claim(ClaimTypeNames.Role, Roles.Operator));
        await new PermissionAuthorizationHandler().HandleAsync(context);
        context.HasSucceeded.Should().BeFalse();
    }
}

// WO-42 / WO-48: TenantsController logic.
public class TenantsControllerTests
{
    private readonly Mock<ITenantRepository> _tenants = new();
    private readonly Mock<ITenantApiConfigurationRepository> _configs = new();
    private readonly Mock<IMappingConfigurationRepository> _mappings = new();
    private readonly Mock<IIntegrationJobRepository> _jobs = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ICredentialEncryptionService> _encryption = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IConcurConnector> _concur = new();
    private readonly Mock<IMaconomyConnector> _maconomy = new();
    private readonly Mock<IAuditTrailService> _audit = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private TenantsController Create() => new(
        _tenants.Object, _configs.Object, _mappings.Object, _jobs.Object, _users.Object, _encryption.Object,
        _tenantContext.Object, _concur.Object, _maconomy.Object, _audit.Object, _unitOfWork.Object);

    [Fact]
    public async Task Create_with_unique_identifier_returns_201_active()
    {
        _tenants.Setup(t => t.IdentifierExistsAsync("acme", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var controller = Create().WithUser(Guid.NewGuid(), Roles.SuperAdmin);
        var result = await controller.Create(new CreateTenantRequest { Name = "Acme", Identifier = "acme" }, default);

        var obj = result.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(StatusCodes.Status201Created);
        obj.Value.Should().BeOfType<ApiResponse<TenantResponse>>().Which.Data!.Status.Should().Be("Active");
    }

    [Fact]
    public async Task Create_with_duplicate_identifier_is_conflict()
    {
        _tenants.Setup(t => t.IdentifierExistsAsync("acme", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var controller = Create().WithUser(Guid.NewGuid(), Roles.SuperAdmin);
        var result = await controller.Create(new CreateTenantRequest { Name = "Acme", Identifier = "acme" }, default);

        var conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflict.Value.Should().BeOfType<ApiErrorResponse>().Which.Error.Code.Should().Be(ApiErrorCodes.DuplicateIdentifier);
    }

    [Fact]
    public async Task Detail_returns_masked_credential_indicators_not_plaintext()
    {
        var tenant = TestData.Tenant();
        _tenants.Setup(t => t.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        _configs.Setup(c => c.ListByTenantAsync(tenant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new TenantApiConfiguration { Id = Guid.NewGuid(), TenantId = tenant.Id, System = SystemName.Concur, EncryptedCredentials = "cipher" } });

        var controller = Create().WithUser(Guid.NewGuid(), Roles.SuperAdmin);
        var result = await controller.GetById(tenant.Id, default);

        var detail = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<ApiResponse<TenantDetail>>().Subject.Data!;
        detail.ConcurConfig.Configured.Should().BeTrue();
        detail.MaconomyConfig.Configured.Should().BeFalse();
    }

    [Fact]
    public async Task Deactivate_sets_inactive_status()
    {
        var tenant = TestData.Tenant();
        _tenants.Setup(t => t.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);

        var controller = Create().WithUser(Guid.NewGuid(), Roles.SuperAdmin);
        await controller.SetStatus(tenant.Id, new UpdateTenantStatusRequest { IsActive = false }, default);

        tenant.Status.Should().Be(TenantStatus.Inactive);
    }

    [Fact]
    public async Task Archive_blocked_when_active_jobs_exist()
    {
        var tenant = TestData.Tenant();
        _tenants.Setup(t => t.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        _jobs.Setup(j => j.HasActiveJobsAsync(tenant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var controller = Create().WithUser(Guid.NewGuid(), Roles.SuperAdmin);
        var result = await controller.Archive(tenant.Id, default);

        result.Should().BeOfType<ConflictObjectResult>().Which.Value
            .Should().BeOfType<ApiErrorResponse>().Which.Error.Code.Should().Be(ApiErrorCodes.ActiveJobsExist);
    }
}
