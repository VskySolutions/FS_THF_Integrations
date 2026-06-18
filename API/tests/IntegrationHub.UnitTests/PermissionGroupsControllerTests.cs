using System.Security.Claims;
using FluentAssertions;
using IntegrationHub.Api.Controllers;
using IntegrationHub.Api.Models.PermissionGroups;
using IntegrationHub.Api.Security;
using IntegrationHub.Application.Abstractions.Auditing;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Security;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Shared.Contracts;
using IntegrationHub.Shared.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace IntegrationHub.UnitTests;

// WO-71: PermissionGroupsController — CRUD, tenant scoping, ceiling enforcement, recompute propagation.
public class PermissionGroupsControllerTests
{
    private readonly Mock<IPermissionGroupRepository> _groups = new();
    private readonly Mock<IRoleRepository> _roles = new();
    private readonly Mock<ITenantRepository> _tenants = new();
    private readonly Mock<IPermissionGroupEffectivePermissionService> _effective = new();
    private readonly Mock<IAuditTrailService> _audit = new();
    private readonly Mock<IAuditTrailRepository> _auditRead = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public PermissionGroupsControllerTests()
    {
        // Default: the tenant's roles carry the full Tenant Admin ceiling, so non-super-admin
        // happy-path tests stay within bounds. Ceiling-specific tests override this.
        _roles.Setup(r => r.ListByTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Role { Id = Guid.NewGuid(), Name = "TA", Permissions = Permissions.ForTenantAdmin().ToList() } });
    }

    private PermissionGroupsController Create() => new(
        _groups.Object, _roles.Object, _tenants.Object, _effective.Object,
        _audit.Object, _auditRead.Object, _unitOfWork.Object);

    private PermissionGroupsController CreateWithUser(string role, Guid? tenantId = null, params string[] permissions)
    {
        var controller = Create();
        var claims = new List<Claim>
        {
            new(ClaimTypeNames.Subject, Guid.NewGuid().ToString()),
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

    private static PermissionGroup Group(Guid tenantId, bool isActive = true, params string[] keys)
    {
        var group = new PermissionGroup { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Existing", IsActive = isActive };
        foreach (var key in keys)
        {
            group.Permissions.Add(new PermissionGroupPermission { Id = Guid.NewGuid(), PermissionGroupId = group.Id, PermissionKey = key });
        }
        return group;
    }

    /// <summary>Extracts the anonymous-typed payload (e.g. new { groupId }) from an ObjectResult's ApiResponse&lt;T&gt;.Data.</summary>
    private static object? DataOf(IActionResult result)
    {
        var value = result.Should().BeAssignableTo<ObjectResult>().Subject.Value!;
        return value.GetType().GetProperty("Data")!.GetValue(value);
    }

    private static T ErrorOf<T>(IActionResult result) => (T)result.Should().BeAssignableTo<ObjectResult>().Subject.Value!;

    // ---- Create ----

    [Fact]
    public async Task Create_success_returns_201_with_group_id_and_stamps_active_tenant()
    {
        var tenantId = Guid.NewGuid();
        PermissionGroup? captured = null;
        _groups.Setup(g => g.AddAsync(It.IsAny<PermissionGroup>(), It.IsAny<CancellationToken>()))
            .Callback<PermissionGroup, CancellationToken>((g, _) => captured = g);
        _groups.Setup(g => g.NameExistsAsync(tenantId, It.IsAny<string>(), Guid.Empty, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var controller = CreateWithUser(Roles.TenantAdmin, tenantId, Permissions.GroupsManage);
        var result = await controller.Create(new CreateGroupRequest
        {
            Name = "Reporting", PermissionKeys = new() { Permissions.JobsRead, Permissions.LogsRead },
        }, default);

        var obj = result.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(StatusCodes.Status201Created);
        captured.Should().NotBeNull();
        captured!.TenantId.Should().Be(tenantId);
        captured.IsActive.Should().BeTrue();
        var groupId = DataOf(result)!.GetType().GetProperty("groupId")!.GetValue(DataOf(result));
        groupId.Should().Be(captured.Id);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_super_admin_with_body_tenant_targets_that_tenant()
    {
        var active = Guid.NewGuid();
        var target = Guid.NewGuid();
        _tenants.Setup(t => t.GetByIdAsync(target, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tenant { Id = target, Name = "T", Identifier = "t", Status = TenantStatus.Active });
        _groups.Setup(g => g.NameExistsAsync(target, It.IsAny<string>(), Guid.Empty, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        PermissionGroup? captured = null;
        _groups.Setup(g => g.AddAsync(It.IsAny<PermissionGroup>(), It.IsAny<CancellationToken>()))
            .Callback<PermissionGroup, CancellationToken>((g, _) => captured = g);

        var controller = CreateWithUser(Roles.SuperAdmin, active, Permissions.GroupsManage);
        var result = await controller.Create(new CreateGroupRequest
        {
            TenantId = target, Name = "Cross", PermissionKeys = new() { Permissions.JobsRead },
        }, default);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status201Created);
        captured!.TenantId.Should().Be(target);
        _tenants.Verify(t => t.GetByIdAsync(target, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_duplicate_name_returns_409_duplicate_group_name()
    {
        var tenantId = Guid.NewGuid();
        _groups.Setup(g => g.NameExistsAsync(tenantId, "Dupe", Guid.Empty, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var controller = CreateWithUser(Roles.TenantAdmin, tenantId, Permissions.GroupsManage);
        var result = await controller.Create(new CreateGroupRequest { Name = "Dupe", PermissionKeys = new() { Permissions.JobsRead } }, default);

        var conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;
        ErrorOf<ApiErrorResponse>(result).Error!.Code.Should().Be(ApiErrorCodes.DuplicateGroupName);
    }

    [Fact]
    public async Task Create_unknown_permission_key_returns_400_validation_failed()
    {
        var tenantId = Guid.NewGuid();

        var controller = CreateWithUser(Roles.TenantAdmin, tenantId, Permissions.GroupsManage);
        var result = await controller.Create(new CreateGroupRequest { Name = "Bad", PermissionKeys = new() { "not.a.real.key" } }, default);

        result.Should().BeOfType<BadRequestObjectResult>();
        ErrorOf<ApiErrorResponse>(result).Error!.Code.Should().Be(ApiErrorCodes.ValidationFailed);
        _groups.Verify(g => g.AddAsync(It.IsAny<PermissionGroup>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_tenant_admin_with_super_admin_only_key_returns_403_ceiling_exceeded()
    {
        var tenantId = Guid.NewGuid();
        // The tenant's roles carry only the Tenant Admin ceiling, so tenants.archive / roles.assign are out of bounds.
        _roles.Setup(r => r.ListByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Role { Id = Guid.NewGuid(), Name = "TA", Permissions = Permissions.ForTenantAdmin().ToList() } });

        var controller = CreateWithUser(Roles.TenantAdmin, tenantId, Permissions.GroupsManage);
        var result = await controller.Create(new CreateGroupRequest
        {
            Name = "Escalate", PermissionKeys = new() { Permissions.TenantsArchive, Permissions.RolesAssign },
        }, default);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        ErrorOf<ApiErrorResponse>(result).Error!.Code.Should().Be(ApiErrorCodes.PermissionCeilingExceeded);
        _groups.Verify(g => g.AddAsync(It.IsAny<PermissionGroup>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_super_admin_with_super_admin_only_keys_succeeds_unrestricted()
    {
        var tenantId = Guid.NewGuid();
        _groups.Setup(g => g.NameExistsAsync(tenantId, It.IsAny<string>(), Guid.Empty, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var controller = CreateWithUser(Roles.SuperAdmin, tenantId, Permissions.GroupsManage);
        var result = await controller.Create(new CreateGroupRequest
        {
            Name = "Powerful", PermissionKeys = new() { Permissions.TenantsArchive, Permissions.RolesAssign },
        }, default);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status201Created);
        // Super Admins are unrestricted: the tenant ceiling is never consulted.
        _roles.Verify(r => r.ListByTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Update ----

    [Fact]
    public async Task Update_triggers_recompute_for_group()
    {
        var tenantId = Guid.NewGuid();
        var group = Group(tenantId, true, Permissions.JobsRead);
        _groups.Setup(g => g.GetByIdAsync(group.Id, It.IsAny<CancellationToken>())).ReturnsAsync(group);
        _groups.Setup(g => g.NameExistsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var controller = CreateWithUser(Roles.TenantAdmin, tenantId, Permissions.GroupsManage);
        var result = await controller.Update(group.Id, new UpdateGroupRequest
        {
            Name = "Renamed", PermissionKeys = new() { Permissions.JobsRead, Permissions.LogsRead },
        }, default);

        result.Should().BeOfType<OkObjectResult>();
        group.Name.Should().Be("Renamed");
        _effective.Verify(e => e.RecomputeForGroupAsync(group.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- SetStatus ----

    [Fact]
    public async Task SetStatus_deactivate_persists_inactive_and_recomputes()
    {
        var tenantId = Guid.NewGuid();
        var group = Group(tenantId, true, Permissions.JobsRead);
        _groups.Setup(g => g.GetByIdAsync(group.Id, It.IsAny<CancellationToken>())).ReturnsAsync(group);

        var controller = CreateWithUser(Roles.TenantAdmin, tenantId, Permissions.GroupsManage);
        var result = await controller.SetStatus(group.Id, new SetGroupStatusRequest { IsActive = false }, default);

        result.Should().BeOfType<OkObjectResult>();
        group.IsActive.Should().BeFalse();
        _groups.Verify(g => g.Update(group), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _effective.Verify(e => e.RecomputeForGroupAsync(group.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- Delete ----

    [Fact]
    public async Task Delete_removes_links_soft_deletes_and_recomputes_each_affected_role()
    {
        var tenantId = Guid.NewGuid();
        var group = Group(tenantId, true, Permissions.JobsRead);
        var roleA = Guid.NewGuid();
        var roleB = Guid.NewGuid();
        _groups.Setup(g => g.GetByIdAsync(group.Id, It.IsAny<CancellationToken>())).ReturnsAsync(group);
        _groups.Setup(g => g.GetRolesUsingGroupAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { (roleA, "A"), (roleB, "B") });
        var links = new[]
        {
            new RolePermissionGroup { Id = Guid.NewGuid(), RoleId = roleA, PermissionGroupId = group.Id },
            new RolePermissionGroup { Id = Guid.NewGuid(), RoleId = roleB, PermissionGroupId = group.Id },
        };
        _groups.Setup(g => g.GetRoleLinksAsync(group.Id, It.IsAny<CancellationToken>())).ReturnsAsync(links);

        var controller = CreateWithUser(Roles.TenantAdmin, tenantId, Permissions.GroupsManage);
        var result = await controller.Delete(group.Id, default);

        result.Should().BeOfType<OkObjectResult>();
        _groups.Verify(g => g.RemoveRoleLink(It.IsAny<RolePermissionGroup>()), Times.Exactly(2));
        _groups.Verify(g => g.Remove(group), Times.Once);
        _effective.Verify(e => e.RecomputeForRoleAsync(roleA, It.IsAny<CancellationToken>()), Times.Once);
        _effective.Verify(e => e.RecomputeForRoleAsync(roleB, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- Detail ----

    [Fact]
    public async Task Get_returns_permission_keys_roles_using_and_audit_trail()
    {
        var tenantId = Guid.NewGuid();
        var group = Group(tenantId, true, Permissions.JobsRead, Permissions.LogsRead);
        _groups.Setup(g => g.GetByIdAsync(group.Id, It.IsAny<CancellationToken>())).ReturnsAsync(group);
        _groups.Setup(g => g.GetRolesUsingGroupAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { (Guid.NewGuid(), "Reporting Role") });
        _auditRead.Setup(a => a.ListByEntityAsync(nameof(PermissionGroup), group.Id.ToString(), 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new AuditTrailEntry { Id = 1, EntityName = nameof(PermissionGroup), EntityId = group.Id.ToString(), Action = "Created", PerformedBy = "admin", CreatedDate = DateTime.UtcNow },
            });

        var controller = CreateWithUser(Roles.TenantAdmin, tenantId, Permissions.GroupsManage);
        var result = await controller.Get(group.Id, default);

        var detail = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<ApiResponse<PermissionGroupDetailResponse>>().Subject.Data!;
        detail.PermissionKeys.Should().BeEquivalentTo(new[] { Permissions.JobsRead, Permissions.LogsRead });
        detail.RolesUsing.Should().ContainSingle(r => r.RoleName == "Reporting Role");
        detail.AuditTrail.Should().ContainSingle(a => a.Action == "Created" && a.PerformedBy == "admin");
    }

    [Fact]
    public async Task Get_unknown_group_returns_404()
    {
        var tenantId = Guid.NewGuid();
        _groups.Setup(g => g.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((PermissionGroup?)null);

        var controller = CreateWithUser(Roles.TenantAdmin, tenantId, Permissions.GroupsManage);
        var result = await controller.Get(Guid.NewGuid(), default);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ---- Permission gate ----

    [Fact]
    public void Controller_requires_groups_manage_permission()
    {
        var attribute = typeof(PermissionGroupsController)
            .GetCustomAttributes(typeof(RequirePermissionAttribute), inherit: true)
            .Cast<RequirePermissionAttribute>()
            .SingleOrDefault();

        attribute.Should().NotBeNull("the controller should be gated by [RequirePermission]");
        attribute!.Policy.Should().EndWith(Permissions.GroupsManage);
    }
}
