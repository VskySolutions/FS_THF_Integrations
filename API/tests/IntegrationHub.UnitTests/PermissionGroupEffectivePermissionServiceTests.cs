using System.Text.Json;
using FluentAssertions;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Security;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Shared.Security;
using Moq;

namespace IntegrationHub.UnitTests;

// WO-71: Effective-permission cache owner. A role's group-derived permissions are the union of the
// keys of all its ACTIVE composed Permission Groups; inactive groups contribute zero.
public class PermissionGroupEffectivePermissionServiceTests
{
    private readonly Mock<IRoleRepository> _roles = new();
    private readonly Mock<IPermissionGroupRepository> _groups = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private PermissionGroupEffectivePermissionService Create()
        => new(_roles.Object, _groups.Object, _unitOfWork.Object);

    private static Role Role(Guid? id = null) => new() { Id = id ?? Guid.NewGuid(), Name = "Role" };

    private static PermissionGroup Group(bool isActive = true, params string[] keys)
    {
        var group = new PermissionGroup { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Name = "G", IsActive = isActive };
        foreach (var key in keys)
        {
            group.Permissions.Add(new PermissionGroupPermission { Id = Guid.NewGuid(), PermissionGroupId = group.Id, PermissionKey = key });
        }
        return group;
    }

    private static IReadOnlyList<string> Parse(string? json)
        => json is null ? Array.Empty<string>() : JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();

    [Fact]
    public async Task RecomputeForRoleAsync_sets_json_to_union_of_active_group_keys_and_persists()
    {
        var role = Role();
        _roles.Setup(r => r.GetByIdAsync(role.Id, It.IsAny<CancellationToken>())).ReturnsAsync(role);
        _groups.Setup(g => g.GetByRoleAsync(role.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Group(true, Permissions.JobsRead, Permissions.LogsRead) });

        await Create().RecomputeForRoleAsync(role.Id, default);

        Parse(role.EffectivePermissionsJson).Should().BeEquivalentTo(Permissions.JobsRead, Permissions.LogsRead);
        _roles.Verify(r => r.Update(role), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecomputeForRoleAsync_inactive_group_contributes_zero()
    {
        var role = Role();
        _roles.Setup(r => r.GetByIdAsync(role.Id, It.IsAny<CancellationToken>())).ReturnsAsync(role);
        _groups.Setup(g => g.GetByRoleAsync(role.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Group(true, Permissions.JobsRead),
                Group(false, Permissions.TenantsWrite, Permissions.RolesAssign),
            });

        await Create().RecomputeForRoleAsync(role.Id, default);

        Parse(role.EffectivePermissionsJson).Should().BeEquivalentTo(Permissions.JobsRead);
    }

    [Fact]
    public async Task RecomputeForRoleAsync_unions_multiple_groups_without_duplicates()
    {
        var role = Role();
        _roles.Setup(r => r.GetByIdAsync(role.Id, It.IsAny<CancellationToken>())).ReturnsAsync(role);
        _groups.Setup(g => g.GetByRoleAsync(role.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Group(true, Permissions.JobsRead, Permissions.LogsRead),
                Group(true, Permissions.LogsRead, Permissions.MappingsRead), // LogsRead overlaps
            });

        await Create().RecomputeForRoleAsync(role.Id, default);

        var keys = Parse(role.EffectivePermissionsJson);
        keys.Should().BeEquivalentTo(new[] { Permissions.JobsRead, Permissions.LogsRead, Permissions.MappingsRead });
        keys.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task RecomputeForRoleAsync_unknown_role_is_a_no_op()
    {
        _roles.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Role?)null);

        await Create().RecomputeForRoleAsync(Guid.NewGuid(), default);

        _roles.Verify(r => r.Update(It.IsAny<Role>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecomputeForGroupAsync_recalculates_every_role_using_the_group_independently()
    {
        var groupId = Guid.NewGuid();
        var roleA = Role();
        var roleB = Role();
        _groups.Setup(g => g.GetRolesUsingGroupAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { (roleA.Id, "A"), (roleB.Id, "B") });
        _roles.Setup(r => r.GetByIdAsync(roleA.Id, It.IsAny<CancellationToken>())).ReturnsAsync(roleA);
        _roles.Setup(r => r.GetByIdAsync(roleB.Id, It.IsAny<CancellationToken>())).ReturnsAsync(roleB);
        _groups.Setup(g => g.GetByRoleAsync(roleA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Group(true, Permissions.JobsRead) });
        _groups.Setup(g => g.GetByRoleAsync(roleB.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Group(true, Permissions.LogsRead, Permissions.MappingsRead) });

        await Create().RecomputeForGroupAsync(groupId, default);

        Parse(roleA.EffectivePermissionsJson).Should().BeEquivalentTo(Permissions.JobsRead);
        Parse(roleB.EffectivePermissionsJson).Should().BeEquivalentTo(Permissions.LogsRead, Permissions.MappingsRead);
        _roles.Verify(r => r.Update(roleA), Times.Once);
        _roles.Verify(r => r.Update(roleB), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PreviewForRoleAsync_returns_live_union_with_inactive_in_sources_but_excluded_from_permissions()
    {
        var roleId = Guid.NewGuid();
        var active = Group(true, Permissions.JobsRead);
        var inactive = Group(false, Permissions.TenantsArchive);
        _groups.Setup(g => g.GetByRoleAsync(roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { active, inactive });

        var preview = await Create().PreviewForRoleAsync(roleId, default);

        preview.Permissions.Should().BeEquivalentTo(Permissions.JobsRead);
        preview.Sources.Should().HaveCount(2);
        preview.Sources.Should().Contain(s => s.GroupId == inactive.Id && !s.IsActive);
        preview.Sources.Single(s => s.GroupId == inactive.Id).Keys.Should().Contain(Permissions.TenantsArchive);
        // Preview is read-only: it must never write the cache.
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
