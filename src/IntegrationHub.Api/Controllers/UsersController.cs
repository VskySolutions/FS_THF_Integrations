using IntegrationHub.Api.Models.Users;
using IntegrationHub.Api.Security;
using IntegrationHub.Application.Abstractions.Auditing;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Security;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Shared.Contracts;
using IntegrationHub.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Api.Controllers;

/// <summary>
/// User account management (WO-38). Super Admins manage all users; Tenant Admins manage
/// Operators/Tenant Admins within their active tenant (REQ-ADM-001/002/003/009/010).
/// </summary>
[ApiController]
public sealed class UsersController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditTrailService _audit;

    public UsersController(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        IAuditTrailService audit)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    [HttpPost("/api/admin/users")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdminOrAbove)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var role = Enum.Parse<UserRole>(request.Role);
        Guid? tenantId;

        if (User.IsSuperAdmin())
        {
            if (role != UserRole.SuperAdmin && request.TenantId is null)
            {
                return BadRequest(ApiResponseFactory.Error(
                    ApiErrorCodes.ValidationFailed, "Validation failed.", "tenantId is required for tenant-scoped roles."));
            }

            tenantId = request.TenantId;
        }
        else
        {
            // Tenant Admin: may only create Operator/Tenant Admin in their own tenant.
            if (role == UserRole.SuperAdmin)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponseFactory.Forbidden("Tenant Admins cannot create Super Admin users."));
            }

            tenantId = User.GetActiveTenantId();
            if (tenantId is null)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponseFactory.Forbidden("No active tenant for the caller."));
            }
        }

        if (await _users.EmailExistsAsync(request.Email, cancellationToken))
        {
            return Conflict(ApiResponseFactory.Error(
                ApiErrorCodes.DuplicateIdentifier, "Email already in use.", request.Email));
        }

        var temporaryPassword = _passwordHasher.GenerateTemporaryPassword();
        var (hash, salt) = _passwordHasher.Hash(temporaryPassword);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            DisplayName = request.DisplayName,
            PasswordHash = hash,
            Salt = salt,
            IsActive = true,
            MustChangePassword = true,
            TokenVersion = 1,
            CreatedDate = DateTime.UtcNow,
        };
        await _users.AddAsync(user, cancellationToken);

        if (tenantId is { } tid)
        {
            await _users.AddAssignmentAsync(new UserTenantRole
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TenantId = tid,
                Role = role,
            }, cancellationToken);
        }

        await _audit.AddAsync(nameof(User), user.Id.ToString(), "Created",
            details: $"role={role}; tenant={tenantId}", cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created,
            ApiResponseFactory.Success(new CreateUserResponse(user.Id, temporaryPassword), "User created."));
    }

    [HttpGet("/api/admin/users")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdminOrAbove)]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);

        Guid? tenantFilter = User.IsSuperAdmin() ? null : User.GetActiveTenantId();
        var (items, total) = await _users.ListAsync(tenantFilter, page, limit, cancellationToken);

        var summaries = items.Select(u => new UserSummary(u.Id, u.Email, u.DisplayName, u.IsActive));
        return Ok(ApiResponseFactory.Paginated(summaries, "Users retrieved.", page, limit, total));
    }

    [HttpGet("/api/admin/users/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdminOrAbove)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(id, cancellationToken);
        if (user is null || !CanCallerSee(user))
        {
            return NotFound(ApiResponseFactory.NotFound("User not found."));
        }

        return Ok(ApiResponseFactory.Success(Map(user), "User retrieved."));
    }

    [HttpPut("/api/admin/users/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.SuperAdminOnly)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound(ApiResponseFactory.NotFound("User not found."));
        }

        if (request.Email is { } email && !string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            if (await _users.EmailExistsAsync(email, cancellationToken))
            {
                return Conflict(ApiResponseFactory.Error(ApiErrorCodes.DuplicateIdentifier, "Email already in use.", email));
            }

            user.Email = email;
            user.TokenVersion++; // email change invalidates sessions
        }

        if (request.DisplayName is { } displayName)
        {
            user.DisplayName = displayName;
        }

        _users.Update(user);
        await _audit.AddAsync(nameof(User), user.Id.ToString(), "Updated", cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponseFactory.Success(Map(user), "User updated."));
    }

    [HttpPut("/api/users/me")]
    [Authorize]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        var user = await _users.GetByIdAsync(userId.Value, cancellationToken);
        if (user is null)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        user.DisplayName = request.DisplayName;
        _users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponseFactory.Success(Map(user), "Profile updated."));
    }

    [HttpPut("/api/admin/users/{id:guid}/status")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdminOrAbove)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] UpdateUserStatusRequest request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound(ApiResponseFactory.NotFound("User not found."));
        }

        var targetIsSuperAdmin = user.TenantRoles.Any(r => r.Role == UserRole.SuperAdmin);
        if (!User.IsSuperAdmin() && (targetIsSuperAdmin || !CanCallerSee(user)))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponseFactory.Forbidden("Not permitted to manage this user."));
        }

        if (!request.IsActive && user.IsActive)
        {
            user.TokenVersion++; // deactivation invalidates sessions
        }

        user.IsActive = request.IsActive;
        _users.Update(user);
        await _audit.AddAsync(nameof(User), user.Id.ToString(),
            request.IsActive ? "Activated" : "Deactivated", cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponseFactory.Success(new { userId = user.Id, isActive = user.IsActive }, "Status updated."));
    }

    private bool CanCallerSee(User user)
    {
        if (User.IsSuperAdmin())
        {
            return true;
        }

        var activeTenant = User.GetActiveTenantId();
        return activeTenant is { } tenant && user.TenantRoles.Any(r => r.TenantId == tenant);
    }

    private static UserDetail Map(User user) => new(
        user.Id,
        user.Email,
        user.DisplayName,
        user.IsActive,
        user.MustChangePassword,
        user.TenantRoles.Select(r => new TenantAssignmentDto(r.TenantId, r.Role.ToString())).ToList());
}
