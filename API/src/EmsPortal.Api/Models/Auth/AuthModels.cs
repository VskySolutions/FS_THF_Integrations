namespace EmsPortal.Api.Models.Auth;

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

/// <summary>Starts the self-service reset. The response never reveals whether the address is known.</summary>
public sealed class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

/// <summary>Redeems an emailed reset token and sets the new password.</summary>
public sealed class ResetPasswordWithTokenRequest
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public sealed record LoginTokenResponse(string AccessToken, int ExpiresIn, string RefreshToken, bool MustChangePassword);

public sealed record RefreshTokenResponse(string AccessToken, int ExpiresIn, string RefreshToken);

public sealed record SwitchTenantResponse(string AccessToken, int ExpiresIn, string TenantIdentifier, IReadOnlyList<string> RoleNames);

public sealed record TenantMembershipDto(Guid TenantId, string Identifier, string Name, IReadOnlyList<string> RoleNames, string TimeZoneId);

public sealed record UserProfileResponse(Guid UserId, string Email, string DisplayName, IReadOnlyList<TenantMembershipDto> Tenants);
