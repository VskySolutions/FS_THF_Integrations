using System.Security.Claims;
using FluentAssertions;
using IntegrationHub.Api.Controllers;
using IntegrationHub.Api.Models.PermissionGroups;
using IntegrationHub.Api.Security;
using IntegrationHub.Application.Abstractions.Auditing;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Security;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Shared.Contracts;
using IntegrationHub.Shared.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace IntegrationHub.UnitTests;

// WO-71: RoleGroupCompositionController — Role ↔ Group composition with cache recompute and dual audit.
public class RoleGroupCompositionControllerTests
{
    private readonly Mock<IRoleRepository> _roles = new();
    private readonly Mock<IPermissionGroupRepository> _groups = new();
    private readonly Mock<IPermissionGroupEffectivePermissionService> _effective = new();
    private readonly Mock<IAuditTrailService> _audit = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private RoleGroupCompositionController Create() => new(
        _roles.Object, _groups.Object, _effective.Object, _audit.Object, _unitOfWork.Object);

    private RoleGroupCompositionController CreateWithUser(string role, Guid? tenantId = null, params string[] permissions)
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

    private static Role Role(Guid? id = null) => new() { Id = id ?? Guid.NewGuid(), Name = "Role" };

    private static PermissionGroup Group(Guid tenantId, bool isActive = true, params string[] keys)
    {
        var group = new PermissionGroup { Id = Guid.NewGuid(), TenantId = tenantId, Name = "G", IsActive = isActive };
        foreach (var key in keys)
        {
            group.Permissions.Add(new PermissionGroupPermission { Id = Guid.NewGuid(), PermissionGroupId = group.Id, PermissionKey = key });
        }
        return group;
    }

    // ---- AssignGroups ----

    [Fact]
    public async Task AssignGroups_adds_links_recomputes_returns_effective_and_writes_both_audit_trails()
    {
        var tenantId = Guid.NewGuid();
        var role = Role();
        var group = Group(tenantId, true, Permissions.JobsRead, Permissions.LogsRead);
        _roles.Setup(r => r.GetByIdAsync(role.Id, It.IsAny<CancellationToken>())).ReturnsAsync(role);
        _groups.Setup(g => g.GetByIdUnscopedAsync(group.Id, It.IsAny<CancellationToken>())).ReturnsAsync(group);
        _groups.Setup(g => g.GetRoleLinkAsync(role.Id, group.Id, It.IsAny<CancellationToken>())).ReturnsAsync((RolePermissionGroup?)null);
        _groups.Setup(g => g.GetByRoleAsync(role.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { group });
        _effective.Setup(e => e.PreviewForRoleAsync(role.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EffectivePermissionPreview(new[] { Permissions.JobsRead, Permissions.LogsRead }, Array.Empty<EffectivePermissionSource>()));

        var controller = CreateWithUser(Roles.TenantAdmin, tenantId, Permissions.RolesWrite);
        var result = await controller.AssignGroups(role.Id, new AssignGroupsRequest { GroupIds = new() { group.Id } }, default);

        var data = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<ApiResponse<RoleGroupsResponse>>().Subject.Data!;
        data.EffectivePermissions.Should().BeEquivalentTo(new[] { Permissions.JobsRead, Permissions.LogsRead });
        _groups.Verify(g => g.AddRoleLinkAsync(It.Is<RolePermissionGroup>(l => l.RoleId == role.Id && l.PermissionGroupId == group.Id), It.IsAny<CancellationToken>()), Times.Once);
        _effective.Verify(e => e.RecomputeForRoleAsync(role.Id, It.IsAny<CancellationToken>()), Times.Once);
        _audit.Verify(a => a.AddAsync(nameof(Role), role.Id.ToString(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _audit.Verify(a => a.AddAsync(nameof(PermissionGroup), group.Id.ToString(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignGroups_groups_from_different_tenants_returns_400()
    {
        var role = Role();
        var groupA = Group(Guid.NewGuid(), true, Permissions.JobsRead);
        var groupB = Group(Guid.NewGuid(), true, Permissions.LogsRead);
        _roles.Setup(r => r.GetByIdAsync(role.Id, It.IsAny<CancellationToken>())).ReturnsAsync(role);
        _groups.Setup(g => g.GetByIdUnscopedAsync(groupA.Id, It.IsAny<CancellationToken>())).ReturnsAsync(groupA);
        _groups.Setup(g => g.GetByIdUnscopedAsync(groupB.Id, It.IsAny<CancellationToken>())).ReturnsAsync(groupB);

        var controller = CreateWithUser(Roles.SuperAdmin, Guid.NewGuid(), Permissions.RolesWrite);
        var result = await controller.AssignGroups(role.Id, new AssignGroupsRequest { GroupIds = new() { groupA.Id, groupB.Id } }, default);

        result.Should().BeOfType<BadRequestObjectResult>();
        _groups.Verify(g => g.AddRoleLinkAsync(It.IsAny<RolePermissionGroup>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AssignGroups_non_super_admin_assigning_another_tenants_group_returns_403()
    {
        var callerTenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var role = Role();
        var group = Group(otherTenant, true, Permissions.JobsRead);
        _roles.Setup(r => r.GetByIdAsync(role.Id, It.IsAny<CancellationToken>())).ReturnsAsync(role);
        _groups.Setup(g => g.GetByIdUnscopedAsync(group.Id, It.IsAny<CancellationToken>())).ReturnsAsync(group);

        var controller = CreateWithUser(Roles.TenantAdmin, callerTenant, Permissions.RolesWrite);
        var result = await controller.AssignGroups(role.Id, new AssignGroupsRequest { GroupIds = new() { group.Id } }, default);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _groups.Verify(g => g.AddRoleLinkAsync(It.IsAny<RolePermissionGroup>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- RemoveGroup ----

    [Fact]
    public async Task RemoveGroup_removes_link_recomputes_and_returns_lost_permissions()
    {
        var tenantId = Guid.NewGuid();
        var role = Role();
        // The removed group provided jobs.read (still covered elsewhere) and logs.read (lost after removal).
        var removed = Group(tenantId, true, Permissions.JobsRead, Permissions.LogsRead);
        var link = new RolePermissionGroup { Id = Guid.NewGuid(), RoleId = role.Id, PermissionGroupId = removed.Id };
        _roles.Setup(r => r.GetByIdAsync(role.Id, It.IsAny<CancellationToken>())).ReturnsAsync(role);
        _groups.Setup(g => g.GetRoleLinkAsync(role.Id, removed.Id, It.IsAny<CancellationToken>())).ReturnsAsync(link);
        _groups.Setup(g => g.GetByIdUnscopedAsync(removed.Id, It.IsAny<CancellationToken>())).ReturnsAsync(removed);
        // Remaining union still covers jobs.read but not logs.read.
        _effective.Setup(e => e.PreviewForRoleAsync(role.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EffectivePermissionPreview(new[] { Permissions.JobsRead }, Array.Empty<EffectivePermissionSource>()));

        var controller = CreateWithUser(Roles.TenantAdmin, tenantId, Permissions.RolesWrite);
        var result = await controller.RemoveGroup(role.Id, removed.Id, default);

        var data = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<ApiResponse<RemoveGroupFromRoleResponse>>().Subject.Data!;
        data.EffectivePermissions.Should().BeEquivalentTo(new[] { Permissions.JobsRead });
        data.LostPermissions.Should().BeEquivalentTo(new[] { Permissions.LogsRead });
        _groups.Verify(g => g.RemoveRoleLink(link), Times.Once);
        _effective.Verify(e => e.RecomputeForRoleAsync(role.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveGroup_when_link_absent_returns_404()
    {
        var role = Role();
        _roles.Setup(r => r.GetByIdAsync(role.Id, It.IsAny<CancellationToken>())).ReturnsAsync(role);
        _groups.Setup(g => g.GetRoleLinkAsync(role.Id, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((RolePermissionGroup?)null);

        var controller = CreateWithUser(Roles.TenantAdmin, Guid.NewGuid(), Permissions.RolesWrite);
        var result = await controller.RemoveGroup(role.Id, Guid.NewGuid(), default);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ---- Preview ----

    [Fact]
    public async Task Preview_returns_live_union_from_the_service()
    {
        var role = Role();
        var group = Group(Guid.NewGuid(), true, Permissions.JobsRead);
        _roles.Setup(r => r.GetByIdAsync(role.Id, It.IsAny<CancellationToken>())).ReturnsAsync(role);
        _effective.Setup(e => e.PreviewForRoleAsync(role.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EffectivePermissionPreview(
                new[] { Permissions.JobsRead },
                new[] { new EffectivePermissionSource(group.Id, group.Name, true, new[] { Permissions.JobsRead }) }));

        var controller = CreateWithUser(Roles.TenantAdmin, Guid.NewGuid(), Permissions.RolesWrite);
        var result = await controller.Preview(role.Id, default);

        var data = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<ApiResponse<RolePermissionPreviewResponse>>().Subject.Data!;
        data.Permissions.Should().BeEquivalentTo(new[] { Permissions.JobsRead });
        data.Sources.Should().ContainSingle(s => s.GroupId == group.Id);
        _effective.Verify(e => e.PreviewForRoleAsync(role.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- Permission gate ----

    [Fact]
    public void Controller_requires_roles_write_permission()
    {
        var attribute = typeof(RoleGroupCompositionController)
            .GetCustomAttributes(typeof(RequirePermissionAttribute), inherit: true)
            .Cast<RequirePermissionAttribute>()
            .SingleOrDefault();

        attribute.Should().NotBeNull("the controller should be gated by [RequirePermission]");
        attribute!.Policy.Should().EndWith(Permissions.RolesWrite);
    }
}
