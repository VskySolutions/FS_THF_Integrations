namespace EmsPortal.Api.Models.Users;

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
    /// <summary>The RBAC roles to assign in the tenant (multi-role). Each must resolve to a known role.</summary>
    public List<Guid> RoleIds { get; set; } = new();
    /// <summary>Legacy single RBAC role. Folded into <see cref="RoleIds"/> for back-compat.</summary>
    public Guid? RoleId { get; set; }
    /// <summary>Legacy fixed-tier role name. Used only when no role ids are supplied (back-compat).</summary>
    public string Role { get; set; } = string.Empty;
    /// <summary>When true, email the new user an invitation with their temporary password (via the tenant's active SMTP account).</summary>
    public bool SendInvitation { get; set; }
    /// <summary>Required job title, from the <c>User.JobTitle</c> option list. Written to the person record.</summary>
    public string? JobTitle { get; set; }
}

public sealed class UpdateUserRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    /// <summary>Job title, from the <c>User.JobTitle</c> option list. Null leaves it unchanged.</summary>
    public string? JobTitle { get; set; }
}

public sealed class UpdateProfileRequest
{
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class UpdateUserStatusRequest
{
    public bool IsActive { get; set; }
}

/// <summary>
/// Reconciles the full set of roles a user holds in a tenant (multi-role). The active assignment set
/// is made to match <see cref="RoleIds"/> — missing roles are added, absent ones soft-deleted; an
/// empty resulting set removes tenant access entirely (AC-ADM-006.2/006.3).
/// </summary>
public sealed class AssignTenantRoleRequest
{
    public Guid TenantId { get; set; }
    /// <summary>The RBAC roles the user should hold in the tenant. Each must resolve to a known role.</summary>
    public List<Guid> RoleIds { get; set; } = new();
    /// <summary>Legacy single RBAC role. Folded into <see cref="RoleIds"/> for back-compat.</summary>
    public Guid? RoleId { get; set; }
    /// <summary>Legacy fixed-tier role name. Used only when no role ids are supplied (back-compat).</summary>
    public string? Role { get; set; }
}

/// <summary>A user's roles within a single tenant (grouped — multi-role).</summary>
public sealed record TenantAssignmentDto(Guid TenantId, IReadOnlyList<TenantAssignmentRoleDto> Roles);

/// <summary>One role held in a tenant: its RBAC id/name plus the legacy fixed-tier shadow.</summary>
public sealed record TenantAssignmentRoleDto(Guid RoleId, string? RoleName, string Role);

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

// ---- Departments ----

/// <summary>
/// Sets (or clears) the user's department within the caller's active tenant. Marking the user as head
/// demotes the department's previous head and repoints the REMS department-director mapping.
/// </summary>
public sealed class SetUserDepartmentRequest
{
    /// <summary>Department code (option-set <c>REMS.Department</c>), or null/empty to unassign the user.</summary>
    public string? Department { get; set; }

    /// <summary>True to make this user the department's head. Ignored when no department is supplied.</summary>
    public bool IsHead { get; set; }
}

/// <summary>Makes (or clears) this user the tenant's REMS managing shareholder — a firm-wide singleton.</summary>
public sealed class SetManagingShareholderRequest
{
    /// <summary>True to hand this user the role (displacing the incumbent); false to clear it.</summary>
    public bool IsManagingShareholder { get; set; }
}

/// <summary>One selectable department for the picker.</summary>
public sealed record DepartmentOptionDto(string Value, string Label);

/// <summary>The current head of a department in the active tenant.</summary>
public sealed record DepartmentHeadDto(string Department, Guid UserId, string FullName);

/// <summary>A minimal user reference (who currently holds a role).</summary>
public sealed record UserRefDto(Guid UserId, string FullName);

/// <summary>
/// Picker data for the user's approval-role section: the tenant's departments, the head of each, and the
/// tenant's managing shareholder — everything needed to name an incumbent before a role is taken over.
/// </summary>
public sealed record DepartmentOptionsResponse(
    IReadOnlyList<DepartmentOptionDto> Departments,
    IReadOnlyList<DepartmentHeadDto> Heads,
    UserRefDto? ManagingShareholder);

/// <summary>The saved role, plus the name of the user it displaced (null when nobody held it).</summary>
public sealed record SetManagingShareholderResponse(bool IsManagingShareholder, string? DisplacedName);

/// <summary>
/// The saved placement, plus the name of the head this change displaced (null when nobody was demoted)
/// so the caller can report the handover.
/// </summary>
public sealed record SetUserDepartmentResponse(string? Department, bool IsHead, string? DemotedHeadName);

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
    string? JobTitle,
    string? TenantName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<UserGroupDto> Groups,
    bool IsActive,
    // The department held in the caller's active tenant — already resolved to its option-set label, and
    // null when the user is unplaced (or the caller has no active tenant). A head is that department's
    // REMS director, which the list flags with an icon.
    string? Department,
    bool IsDepartmentHead,
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
    // From the person record, chosen from the User.JobTitle option list (the label is what is stored).
    string? JobTitle,
    bool IsActive,
    bool MustChangePassword,
    IReadOnlyList<TenantAssignmentDto> Assignments,
    IReadOnlyList<UserGroupDto> Groups,
    // The department held in the active tenant (null when unassigned), and whether the user heads it —
    // a head is also the department's REMS director. IsManagingShareholder is the tenant-wide REMS role.
    string? Department,
    bool IsDepartmentHead,
    bool IsManagingShareholder);
