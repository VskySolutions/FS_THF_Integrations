using EmsPortal.Domain.Entities;

namespace EmsPortal.Application.Abstractions.Persistence;

/// <summary>Data access for tenant-scoped <see cref="UserGroup"/>s and their memberships.</summary>
public interface IUserGroupRepository
{
    /// <summary>All groups in the active tenant, optionally filtered by a name search.</summary>
    Task<IReadOnlyList<UserGroup>> ListAsync(string? search, CancellationToken cancellationToken = default);

    Task<UserGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Finds a group by exact (case-insensitive) name within the active tenant.</summary>
    Task<UserGroup?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task AddAsync(UserGroup group, CancellationToken cancellationToken = default);

    void Remove(UserGroup group);

    /// <summary>The user's active group memberships in the active tenant.</summary>
    Task<IReadOnlyList<UserGroupMember>> GetMembershipsForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>All active memberships of a group (used to clean them up when the group is deleted).</summary>
    Task<IReadOnlyList<UserGroupMember>> GetMembersByGroupAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>A group's active memberships with the linked <see cref="User"/> loaded (for the members list).</summary>
    Task<IReadOnlyList<UserGroupMember>> GetMembersWithUsersByGroupAsync(Guid groupId, CancellationToken cancellationToken = default);

    Task AddMemberAsync(UserGroupMember member, CancellationToken cancellationToken = default);

    void RemoveMember(UserGroupMember member);

    /// <summary>Member count per group id (for the groups list).</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetMemberCountsAsync(CancellationToken cancellationToken = default);
}
