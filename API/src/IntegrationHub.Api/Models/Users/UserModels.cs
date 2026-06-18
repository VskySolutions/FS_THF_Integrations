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

public sealed record CreateUserResponse(Guid UserId, string TemporaryPassword);

public sealed record ResetPasswordResponse(Guid UserId, string TemporaryPassword);

public sealed record UserSummary(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    string? PhoneNumber,
    string? TenantName,
    IReadOnlyList<string> Roles,
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
    IReadOnlyList<TenantAssignmentDto> Assignments);
