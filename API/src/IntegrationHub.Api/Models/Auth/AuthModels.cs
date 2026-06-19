namespace IntegrationHub.Api.Models.Auth;

public sealed class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class RefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class LogoutRequest
{
    public string? RefreshToken { get; set; }
}

public sealed class SwitchTenantRequest
{
    public Guid TenantId { get; set; }
}

public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public sealed record LoginTokenResponse(string AccessToken, int ExpiresIn, string RefreshToken, bool MustChangePassword);

public sealed record RefreshTokenResponse(string AccessToken, int ExpiresIn, string RefreshToken);

public sealed record SwitchTenantResponse(string AccessToken, int ExpiresIn, string TenantIdentifier, string Role);

public sealed record TenantMembershipDto(Guid TenantId, string Identifier, string Name, string Role, string? RoleName, string TimeZoneId);

public sealed record UserProfileResponse(Guid UserId, string Email, string DisplayName, IReadOnlyList<TenantMembershipDto> Tenants);
