using System.Security.Cryptography;
using System.Text;
using EmsPortal.Api.Models.Auth;
using EmsPortal.Api.Security;
using EmsPortal.Application.Abstractions.Email;
using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Application.Abstractions.Security;
using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;
using EmsPortal.Shared.Configuration;
using EmsPortal.Shared.Contracts;
using EmsPortal.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace EmsPortal.Api.Controllers;

/// <summary>
/// Authentication and session management (WO-39): login, refresh, logout, logout-all,
/// tenant switch, profile, change-password (Admin User &amp; Role Management).
/// </summary>
[ApiController]
[Produces("application/json")]
[Tags("Auth")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status500InternalServerError)]
public sealed class AuthController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwt;
    private readonly ITenantRepository _tenants;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuthenticationOptions _options;
    private readonly IEmailDispatcher _emailDispatcher;

    public AuthController(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwt,
        ITenantRepository tenants,
        IUnitOfWork unitOfWork,
        IOptions<AuthenticationOptions> options,
        IEmailDispatcher emailDispatcher)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _passwordHasher = passwordHasher;
        _jwt = jwt;
        _tenants = tenants;
        _unitOfWork = unitOfWork;
        _options = options.Value;
        _emailDispatcher = emailDispatcher;
    }

    [HttpPost("/api/auth/login")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<LoginTokenResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash, user.Salt))
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("Invalid email or password."));
        }

        if (!user.IsActive)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("Account is disabled."));
        }

        var activeTenantId = user.TenantRoles.FirstOrDefault()?.TenantId ?? Guid.Empty;
        var access = _jwt.CreateAccessToken(user, activeTenantId);
        var refresh = await IssueRefreshTokenAsync(user.Id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponseFactory.Success(
            new LoginTokenResponse(access.Token, access.ExpiresInSeconds, refresh, user.MustChangePassword),
            "Login successful."));
    }

    [HttpPost("/api/auth/refresh")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<RefreshTokenResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        var stored = await _refreshTokens.GetByHashAsync(HashToken(request.RefreshToken), cancellationToken);
        if (stored is null || stored.IsRevoked || stored.ExpiresAt <= DateTime.UtcNow)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("Refresh token is invalid or expired."));
        }

        var user = await _users.GetByIdAsync(stored.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("Account is disabled."));
        }

        // Rotate: revoke the presented token, issue a new one.
        stored.IsRevoked = true;
        _refreshTokens.Update(stored);
        var newRefresh = await IssueRefreshTokenAsync(user.Id, cancellationToken);

        var activeTenantId = user.TenantRoles.FirstOrDefault()?.TenantId ?? Guid.Empty;
        var access = _jwt.CreateAccessToken(user, activeTenantId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponseFactory.Success(
            new RefreshTokenResponse(access.Token, access.ExpiresInSeconds, newRefresh), "Token refreshed."));
    }

    [HttpPost("/api/auth/logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        user.TokenVersion++; // invalidates all access tokens
        _users.Update(user);

        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var stored = await _refreshTokens.GetByHashAsync(HashToken(request.RefreshToken), cancellationToken);
            if (stored is not null && stored.UserId == user.Id)
            {
                stored.IsRevoked = true;
                _refreshTokens.Update(stored);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(new { message = "Logged out." }, "Logged out."));
    }

    [HttpPost("/api/auth/logout-all")]
    [Authorize]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        user.TokenVersion++;
        _users.Update(user);
        await _refreshTokens.RevokeAllForUserAsync(user.Id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponseFactory.Success(new { message = "Logged out everywhere." }, "Logged out everywhere."));
    }

    [HttpPost("/api/auth/switch-tenant")]
    [Authorize]
    [ProducesResponseType<ApiResponse<SwitchTenantResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SwitchTenant([FromBody] SwitchTenantRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        var isSuperAdmin = user.TenantRoles.Any(r => r.Role == UserRole.SuperAdmin);
        var assignment = user.TenantRoles.FirstOrDefault(r => r.TenantId == request.TenantId);
        if (!isSuperAdmin && assignment is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponseFactory.Forbidden("You are not assigned to the requested tenant."));
        }

        var tenant = await _tenants.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponseFactory.Forbidden("Tenant not found."));
        }

        var access = _jwt.CreateAccessToken(user, request.TenantId);
        var role = isSuperAdmin ? Roles.SuperAdmin : assignment!.Role.ToString();
        // Refresh token is intentionally not rotated on switch (AC-ADM-011.5).
        return Ok(ApiResponseFactory.Success(
            new SwitchTenantResponse(access.Token, access.ExpiresInSeconds, tenant.Identifier, role), "Tenant switched."));
    }

    [HttpGet("/api/auth/profile")]
    [Authorize]
    [ProducesResponseType<ApiResponse<UserProfileResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        var memberships = new List<TenantMembershipDto>();
        foreach (var assignment in user.TenantRoles)
        {
            var tenant = await _tenants.GetByIdAsync(assignment.TenantId, cancellationToken);
            memberships.Add(new TenantMembershipDto(
                assignment.TenantId,
                tenant?.Identifier ?? string.Empty,
                tenant?.Name ?? string.Empty,
                assignment.Role.ToString(),
                // The RBAC role name (custom roles); falls back to the legacy enum for display.
                assignment.RoleEntity?.Name ?? assignment.Role.ToString(),
                tenant?.TimeZoneId ?? "UTC"));
        }

        return Ok(ApiResponseFactory.Success(
            new UserProfileResponse(user.Id, user.Email, user.DisplayName, memberships), "Profile retrieved."));
    }

    [HttpPut("/api/users/me/change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash, user.Salt))
        {
            return BadRequest(ApiResponseFactory.Error(
                ApiErrorCodes.ValidationFailed, "Validation failed.", "Current password is incorrect."));
        }

        var (hash, salt) = _passwordHasher.Hash(request.NewPassword);
        user.PasswordHash = hash;
        user.Salt = salt;
        user.MustChangePassword = false;
        user.TokenVersion++; // invalidate all sessions
        _users.Update(user);
        await _refreshTokens.RevokeAllForUserAsync(user.Id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Best-effort confirmation email via the user's tenant active SMTP account.
        var tenantForEmail = User.GetActiveTenantId() ?? user.TenantRoles.FirstOrDefault()?.TenantId;
        if (tenantForEmail is { } tid)
        {
            _emailDispatcher.Enqueue(tid, EmailTemplateKey.PasswordChanged, user.Email,
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["FullName"] = user.DisplayName,
                    ["ChangedAtUtc"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"),
                });
        }

        return Ok(ApiResponseFactory.Success(new { message = "Password changed." }, "Password changed."));
    }

    private Task<User?> CurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        return userId is null ? Task.FromResult<User?>(null) : _users.GetByIdAsync(userId.Value, cancellationToken);
    }

    private async Task<string> IssueRefreshTokenAsync(Guid userId, CancellationToken cancellationToken)
    {
        var plaintext = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await _refreshTokens.AddAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = HashToken(plaintext),
            ExpiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenDays <= 0 ? 7 : _options.RefreshTokenDays),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow,
        }, cancellationToken);
        return plaintext;
    }

    private static string HashToken(string token)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
