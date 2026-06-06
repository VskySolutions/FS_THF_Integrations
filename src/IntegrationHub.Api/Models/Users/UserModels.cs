namespace IntegrationHub.Api.Models.Users;

public sealed class CreateUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    /// <summary>Target tenant. Ignored for Tenant Admins (forced to their active tenant).</summary>
    public Guid? TenantId { get; set; }
    public string Role { get; set; } = string.Empty;
}

public sealed class UpdateUserRequest
{
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
    public string Role { get; set; } = string.Empty;
}

public sealed record TenantAssignmentDto(Guid TenantId, string Role);

public sealed record CreateUserResponse(Guid UserId, string TemporaryPassword);

public sealed record UserSummary(Guid UserId, string Email, string DisplayName, bool IsActive);

public sealed record UserDetail(
    Guid UserId,
    string Email,
    string DisplayName,
    bool IsActive,
    bool MustChangePassword,
    IReadOnlyList<TenantAssignmentDto> Assignments);
