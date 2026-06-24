namespace IntegrationHub.Api.Models.Users;

public sealed class CreateUserRequest
{
    /// <summary>The existing Person to promote to a login account. Required (people are created first).</summary>
    public Guid PersonId { get; set; }
    /// <summary>Login email/username. Defaults to the person's primary email when omitted.</summary>
    public string? Email { get; set; }
    /// <summary>Optional phone; when supplied it is written back to the person's mobile number.</summary>
    public string? PhoneNumber { get; set; }
    /// <summary>Optional dial code for <see cref="PhoneNumber"/>, written back to the person.</summary>
    public string? CountryCode { get; set; }
    /// <summary>Target tenant. Ignored for Tenant Admins (forced to their active tenant).</summary>
    public Guid? TenantId { get; set; }
    /// <summary>Legacy fixed-tier role. Optional when <see cref="RoleId"/> is supplied.</summary>
    public string Role { get; set; } = string.Empty;
    /// <summary>RBAC role to assign. When set, takes precedence and must be available to the tenant.</summary>
    public Guid? RoleId { get; set; }
    /// <summary>When true, email the new user an invitation with their temporary password (via the tenant's active SMTP account).</summary>
    public bool SendInvitation { get; set; }
}

public sealed class UpdateUserRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
}

public sealed class UpdateProfileRequest
{
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class UpdateUserStatusRequest
{
    public bool IsActive { get; set; }
}

public sealed class AssignTenantRoleRequest
{
    public Guid TenantId { get; set; }
    /// <summary>Legacy fixed-tier role. Optional when <see cref="RoleId"/> is supplied.</summary>
    public string? Role { get; set; }
    /// <summary>RBAC role to assign. When set, takes precedence and must be available to the tenant.</summary>
    public Guid? RoleId { get; set; }
}

public sealed record TenantAssignmentDto(Guid TenantId, string Role, Guid? RoleId, string? RoleName);

// ---- User groups ----

/// <summary>Lightweight reference to a user group (used on user summaries/details).</summary>
public sealed record UserGroupDto(Guid Id, string Name);

/// <summary>A user group with its member count + provenance (for the groups picker / management list).</summary>
public sealed record UserGroupResponse(
    Guid Id, string Name, string? Description, int MemberCount, string? CreatedBy, DateTime CreatedOnUtc);

public sealed class CreateUserGroupRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>Replaces a user's group memberships with the given set.</summary>
public sealed class AssignUserGroupsRequest
{
    public List<Guid> GroupIds { get; set; } = new();
}

/// <summary>A member (user) of a group, with who added them and when — for the group's members list.</summary>
public sealed record UserGroupMemberResponse(
    Guid UserId, string FullName, string? Email, bool IsActive, string? AddedBy, DateTime AddedOnUtc);

/// <summary>Adds the given users to a group (members already present are ignored).</summary>
public sealed class AddGroupMembersRequest
{
    public List<Guid> UserIds { get; set; } = new();
}

public sealed record CreateUserResponse(Guid UserId, string TemporaryPassword, bool InvitationEmailSent);

public sealed record ResetPasswordResponse(Guid UserId, string TemporaryPassword, bool EmailSent);

public sealed record UserSummary(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    string? PhoneNumber,
    string? TenantName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<UserGroupDto> Groups,
    bool IsActive,
    string? CreatedBy,
    string? UpdatedBy,
    DateTime CreatedOnUtc,
    DateTime UpdatedOnUtc);

public sealed record UserDetail(
    Guid UserId,
    Guid? PersonId,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    string? PhoneNumber,
    string DisplayName,
    bool IsActive,
    bool MustChangePassword,
    IReadOnlyList<TenantAssignmentDto> Assignments,
    IReadOnlyList<UserGroupDto> Groups);
