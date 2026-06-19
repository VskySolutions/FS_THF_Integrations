using IntegrationHub.Api.Models.Users;
using IntegrationHub.Api.Security;
using IntegrationHub.Application.Abstractions.Auditing;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Shared.Contracts;
using IntegrationHub.Shared.Security;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Api.Controllers;

/// <summary>
/// Tenant-scoped user groups: a way to segment/tag users (independent of RBAC roles) so they can be
/// listed and filtered by group name. Listing requires <c>users.read</c>; creating requires
/// <c>users.write</c> (assigning groups to a user lives on the Users endpoint).
/// </summary>
[ApiController]
[Route("/api/admin/user-groups")]
[Produces("application/json")]
[Tags("User Groups")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status500InternalServerError)]
public sealed class UserGroupsController : ControllerBase
{
    private readonly IUserGroupRepository _groups;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditTrailService _audit;

    public UserGroupsController(IUserGroupRepository groups, IUserRepository users, IUnitOfWork unitOfWork, IAuditTrailService audit)
    {
        _groups = groups;
        _users = users;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    [HttpGet]
    [RequirePermission(Permissions.UsersRead)]
    public async Task<IActionResult> List([FromQuery] string? search = null, CancellationToken cancellationToken = default)
    {
        var groups = await _groups.ListAsync(search, cancellationToken);
        var counts = await _groups.GetMemberCountsAsync(cancellationToken);
        // Resolve creator ids to display names for the "created by" tooltip.
        var creatorNames = await _users.GetFullNamesAsync(
            groups.Where(g => g.CreatedById.HasValue).Select(g => g.CreatedById!.Value), cancellationToken);
        var data = groups.Select(g => new UserGroupResponse(
            g.Id, g.Name, g.Description, counts.TryGetValue(g.Id, out var c) ? c : 0,
            g.CreatedById is { } cid && creatorNames.TryGetValue(cid, out var name) ? name : null, g.CreatedOnUtc));
        return Ok(ApiResponseFactory.Success(data, "User groups retrieved."));
    }

    [HttpPost]
    [RequirePermission(Permissions.UsersGroupManagement)]
    [ProducesResponseType<ApiResponse<UserGroupResponse>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateUserGroupRequest request, CancellationToken cancellationToken)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed, "Validation failed.", "Group name is required."));
        }

        // If the name already exists in this tenant, return it (so the inline "create" in the picker
        // simply resolves to the existing group rather than failing on the unique index).
        if (await _groups.GetByNameAsync(name, cancellationToken) is { } existing)
        {
            return Ok(ApiResponseFactory.Success(
                new UserGroupResponse(existing.Id, existing.Name, existing.Description, 0, null, existing.CreatedOnUtc), "Group already exists."));
        }

        var group = new UserGroup { Id = Guid.NewGuid(), Name = name, Description = request.Description?.Trim() };
        await _groups.AddAsync(group, cancellationToken);
        await _audit.AddAsync(nameof(UserGroup), group.Id.ToString(), "Created", details: name, cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created,
            ApiResponseFactory.Success(new UserGroupResponse(group.Id, group.Name, group.Description, 0, null, group.CreatedOnUtc), "User group created."));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.UsersGroupManagement)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var group = await _groups.GetByIdAsync(id, cancellationToken);
        if (group is null)
        {
            return NotFound(ApiResponseFactory.NotFound("User group not found."));
        }

        // Soft-delete the memberships first, then the group itself.
        foreach (var member in await _groups.GetMembersByGroupAsync(id, cancellationToken))
        {
            _groups.RemoveMember(member);
        }
        _groups.Remove(group);
        await _audit.AddAsync(nameof(UserGroup), id.ToString(), "Deleted", details: group.Name, cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponseFactory.Success(new { groupId = id }, "User group deleted."));
    }

    // ---- Members ----

    [HttpGet("{id:guid}/members")]
    [RequirePermission(Permissions.UsersRead)]
    public async Task<IActionResult> Members(Guid id, CancellationToken cancellationToken)
    {
        if (await _groups.GetByIdAsync(id, cancellationToken) is null)
        {
            return NotFound(ApiResponseFactory.NotFound("User group not found."));
        }

        var members = (await _groups.GetMembersWithUsersByGroupAsync(id, cancellationToken)).Where(m => m.User is not null).ToList();
        // Resolve both the member's name and the name of whoever added them to the group.
        var names = await _users.GetFullNamesAsync(
            members.Select(m => m.UserId).Concat(members.Where(m => m.CreatedById.HasValue).Select(m => m.CreatedById!.Value)),
            cancellationToken);
        var data = members.Select(m => new UserGroupMemberResponse(
            m.UserId,
            names.TryGetValue(m.UserId, out var name) ? name : m.User!.DisplayName,
            m.User!.Email,
            m.User!.IsActive,
            m.CreatedById is { } addedById && names.TryGetValue(addedById, out var addedBy) ? addedBy : null,
            m.CreatedOnUtc));
        return Ok(ApiResponseFactory.Success(data, "Group members retrieved."));
    }

    [HttpPost("{id:guid}/members")]
    [RequirePermission(Permissions.UsersGroupManagement)]
    public async Task<IActionResult> AddMembers(Guid id, [FromBody] AddGroupMembersRequest request, CancellationToken cancellationToken)
    {
        if (await _groups.GetByIdAsync(id, cancellationToken) is null)
        {
            return NotFound(ApiResponseFactory.NotFound("User group not found."));
        }

        var existing = (await _groups.GetMembersByGroupAsync(id, cancellationToken)).Select(m => m.UserId).ToHashSet();
        foreach (var userId in (request.UserIds ?? new List<Guid>()).Distinct().Where(u => !existing.Contains(u)))
        {
            await _groups.AddMemberAsync(new UserGroupMember { Id = Guid.NewGuid(), UserGroupId = id, UserId = userId }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(new { groupId = id }, "Members added."));
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [RequirePermission(Permissions.UsersGroupManagement)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var member = (await _groups.GetMembersByGroupAsync(id, cancellationToken)).FirstOrDefault(m => m.UserId == userId);
        if (member is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Membership not found."));
        }

        _groups.RemoveMember(member);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(new { groupId = id, userId }, "Member removed."));
    }
}
