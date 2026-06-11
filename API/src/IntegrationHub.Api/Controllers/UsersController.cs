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
[Produces("application/json")]
[Tags("Users")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status500InternalServerError)]
public sealed class UsersController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditTrailService _audit;
    private readonly IRoleRepository _roles;
    private readonly IPersonRepository _persons;

    public UsersController(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IRefreshTokenRepository refreshTokens,
        IUnitOfWork unitOfWork,
        IAuditTrailService audit,
        IRoleRepository roles,
        IPersonRepository persons)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _refreshTokens = refreshTokens;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _roles = roles;
        _persons = persons;
    }

    [HttpPost("/api/admin/users")]
    [RequirePermission(Permissions.UsersWrite)]
    [ProducesResponseType<ApiResponse<CreateUserResponse>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var (role, roleEntity, roleError) = await ResolveRequestRoleAsync(request.RoleId, request.Role, cancellationToken);
        if (roleError is not null)
        {
            return roleError;
        }

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

        var fullName = string.Join(" ", new[] { request.FirstName, request.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var displayName = string.IsNullOrWhiteSpace(fullName) ? (request.DisplayName ?? string.Empty) : fullName;

        // Personal profile data lives on the Person master record (WO-61).
        var userId = Guid.NewGuid();
        var person = new Person
        {
            Id = Guid.NewGuid(),
            PersonCode = await GeneratePersonCodeAsync(cancellationToken),
            UserId = userId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            DisplayName = displayName,
            MobileNumber = request.PhoneNumber,
            PrimaryEmail = request.Email,
            IsActive = true,
        };
        await _persons.AddAsync(person, cancellationToken);

        var user = new User
        {
            Id = userId,
            Email = request.Email,
            DisplayName = displayName,
            PersonId = person.Id,
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
            var (assignedRoleId, availabilityError) = await ResolveAssignmentRoleIdAsync(role, roleEntity, tid, cancellationToken);
            if (availabilityError is not null)
            {
                return availabilityError;
            }

            await _users.AddAssignmentAsync(new UserTenantRole
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TenantId = tid,
                Role = role,
                RoleId = assignedRoleId,
            }, cancellationToken);
        }

        await _audit.AddAsync(nameof(User), user.Id.ToString(), "Created",
            details: $"role={role}; tenant={tenantId}", cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created,
            ApiResponseFactory.Success(new CreateUserResponse(user.Id, temporaryPassword), "User created."));
    }

    [HttpGet("/api/admin/users")]
    [RequirePermission(Permissions.UsersRead)]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);

        Guid? tenantFilter = User.IsSuperAdmin() ? null : User.GetActiveTenantId();
        var (items, total) = await _users.ListAsync(tenantFilter, page, limit, cancellationToken);

        var names = await ResolveActorNamesAsync(items.SelectMany(u => new[] { u.CreatedById, u.UpdatedById }), cancellationToken);
        var summaries = items.Select(u => new UserSummary(
            u.Id, u.Email,
            u.Person?.FirstName ?? string.Empty, u.Person?.LastName ?? string.Empty,
            u.Person?.FullName ?? u.DisplayName, u.Person?.MobileNumber, u.IsActive,
            NameOf(names, u.CreatedById), NameOf(names, u.UpdatedById), u.CreatedOnUtc, u.UpdatedOnUtc));
        return Ok(ApiResponseFactory.Paginated(summaries, "Users retrieved.", page, limit, total));
    }

    [HttpGet("/api/admin/users/{id:guid}")]
    [RequirePermission(Permissions.UsersRead)]
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
    [RequirePermission(Permissions.UsersWrite)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound(ApiResponseFactory.NotFound("User not found."));
        }

        // Non-Super-Admins may only edit users within their active tenant, and never a Super Admin.
        var targetIsSuperAdmin = user.TenantRoles.Any(r => r.Role == UserRole.SuperAdmin);
        if (!User.IsSuperAdmin() && (targetIsSuperAdmin || !CanCallerSee(user)))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponseFactory.Forbidden("Not permitted to manage this user."));
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

        // Personal fields live on the Person record (WO-61).
        var person = user.Person;
        if (person is not null)
        {
            if (request.FirstName is not null)
            {
                person.FirstName = request.FirstName;
            }
            if (request.LastName is not null)
            {
                person.LastName = request.LastName;
            }
            if (request.PhoneNumber is not null)
            {
                person.MobileNumber = request.PhoneNumber;
            }

            // Keep DisplayName in sync with the name unless explicitly overridden.
            if (request.DisplayName is { } displayName)
            {
                person.DisplayName = displayName;
                user.DisplayName = displayName;
            }
            else if (request.FirstName is not null || request.LastName is not null)
            {
                person.DisplayName = person.FullName;
                user.DisplayName = person.FullName;
            }

            _persons.Update(person);
        }
        else if (request.DisplayName is { } displayNameOnly)
        {
            user.DisplayName = displayNameOnly;
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
        if (user.Person is not null)
        {
            user.Person.DisplayName = request.DisplayName;
            _persons.Update(user.Person);
        }
        _users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponseFactory.Success(Map(user), "Profile updated."));
    }

    [HttpPut("/api/admin/users/{id:guid}/status")]
    [RequirePermission(Permissions.UsersWrite)]
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

    [HttpPost("/api/admin/users/{id:guid}/reset-password")]
    [RequirePermission(Permissions.UsersResetPassword)]
    [ProducesResponseType<ApiResponse<ResetPasswordResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetPassword(Guid id, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(id, cancellationToken);
        if (user is null || !CanCallerSee(user))
        {
            return NotFound(ApiResponseFactory.NotFound("User not found."));
        }

        // AC-ADM-013.3: a Tenant Admin may not reset a Super Admin's password.
        var targetIsSuperAdmin = user.TenantRoles.Any(r => r.Role == UserRole.SuperAdmin);
        if (!User.IsSuperAdmin() && targetIsSuperAdmin)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponseFactory.Forbidden("Tenant Admins cannot reset a Super Admin's password."));
        }

        // AC-ADM-013.1/013.4: new temporary password, force change on next login.
        var temporaryPassword = _passwordHasher.GenerateTemporaryPassword();
        var (hash, salt) = _passwordHasher.Hash(temporaryPassword);
        user.PasswordHash = hash;
        user.Salt = salt;
        user.MustChangePassword = true;
        user.TokenVersion++; // AC-ADM-013.5: invalidate all existing sessions
        _users.Update(user);
        await _refreshTokens.RevokeAllForUserAsync(user.Id, cancellationToken);
        await _audit.AddAsync(nameof(User), user.Id.ToString(), "PasswordReset", cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // AC-ADM-013.2: the plaintext temporary password is returned only in this response.
        return Ok(ApiResponseFactory.Success(
            new ResetPasswordResponse(user.Id, temporaryPassword), "Password reset."));
    }

    [HttpPost("/api/admin/users/{id:guid}/tenant-assignments")]
    [RequirePermission(Permissions.RolesAssign)]
    public async Task<IActionResult> AssignTenantRole(Guid id, [FromBody] AssignTenantRoleRequest request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound(ApiResponseFactory.NotFound("User not found."));
        }

        // Tenant Admins are scoped to their active tenant (Super Admins are unrestricted).
        if (!User.IsSuperAdmin())
        {
            var activeTenant = User.GetActiveTenantId();
            if (activeTenant is null || request.TenantId != activeTenant)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponseFactory.Forbidden("Tenant Admins can only assign roles within their active tenant."));
            }

            if (user.TenantRoles.Any(r => r.Role == UserRole.SuperAdmin))
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponseFactory.Forbidden("Tenant Admins cannot manage Super Admin users."));
            }
        }

        var (role, roleEntity, roleError) = await ResolveRequestRoleAsync(request.RoleId, request.Role, cancellationToken);
        if (roleError is not null)
        {
            return roleError;
        }

        if (!User.IsSuperAdmin() && role == UserRole.SuperAdmin)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponseFactory.Forbidden("Tenant Admins cannot assign the Super Admin role."));
        }

        var (assignedRoleId, availabilityError) = await ResolveAssignmentRoleIdAsync(role, roleEntity, request.TenantId, cancellationToken);
        if (availabilityError is not null)
        {
            return availabilityError;
        }

        var existing = await _users.GetAssignmentAsync(id, request.TenantId, cancellationToken);
        if (existing is not null)
        {
            // reassignment updates rather than duplicates (AC-ADM-006.2)
            existing.Role = role;
            existing.RoleId = assignedRoleId;
        }
        else
        {
            await _users.AddAssignmentAsync(new UserTenantRole
            {
                Id = Guid.NewGuid(),
                UserId = id,
                TenantId = request.TenantId,
                Role = role,
                RoleId = assignedRoleId,
            }, cancellationToken);
        }

        await _audit.AddAsync(nameof(User), id.ToString(), "TenantRoleAssigned",
            details: $"tenant={request.TenantId}; role={role}", cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponseFactory.Success(new { userId = id, tenantId = request.TenantId, role = role.ToString() }, "Assignment saved."));
    }

    [HttpDelete("/api/admin/users/{id:guid}/tenant-assignments/{tenantId:guid}")]
    [RequirePermission(Permissions.RolesAssign)]
    public async Task<IActionResult> RemoveTenantRole(Guid id, Guid tenantId, CancellationToken cancellationToken)
    {
        // Tenant Admins are scoped to their active tenant (Super Admins are unrestricted).
        if (!User.IsSuperAdmin() && User.GetActiveTenantId() != tenantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponseFactory.Forbidden("Tenant Admins can only remove roles within their active tenant."));
        }

        var existing = await _users.GetAssignmentAsync(id, tenantId, cancellationToken);
        if (existing is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Assignment not found."));
        }

        if (!User.IsSuperAdmin() && existing.Role == UserRole.SuperAdmin)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponseFactory.Forbidden("Tenant Admins cannot remove a Super Admin assignment."));
        }

        _users.RemoveAssignment(existing);
        await _audit.AddAsync(nameof(User), id.ToString(), "TenantRoleRemoved",
            details: $"tenant={tenantId}", cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponseFactory.Success(new { message = "Assignment removed." }, "Assignment removed."));
    }

    /// <summary>
    /// Resolves the role to assign from either an explicit <paramref name="roleId"/> (RBAC) or the
    /// legacy <paramref name="roleName"/> enum, returning the legacy enum (for transition-era policies)
    /// and the loaded RBAC role when one was supplied.
    /// </summary>
    private async Task<(UserRole Role, Role? RoleEntity, IActionResult? Error)> ResolveRequestRoleAsync(
        Guid? roleId, string? roleName, CancellationToken cancellationToken)
    {
        if (roleId is { } rid)
        {
            var roleEntity = await _roles.GetByIdAsync(rid, cancellationToken);
            if (roleEntity is null)
            {
                return (default, null, BadRequest(ApiResponseFactory.Error(
                    ApiErrorCodes.ValidationFailed, "Validation failed.", "Unknown roleId.")));
            }

            return (MapLegacyRole(roleEntity, roleName), roleEntity, null);
        }

        if (string.IsNullOrWhiteSpace(roleName) || !Enum.TryParse<UserRole>(roleName, ignoreCase: false, out var enumValue))
        {
            return (default, null, BadRequest(ApiResponseFactory.Error(
                ApiErrorCodes.ValidationFailed, "Validation failed.", "A valid role or roleId is required.")));
        }

        return (enumValue, null, null);
    }

    /// <summary>
    /// Determines the <see cref="UserTenantRole.RoleId"/> to persist and enforces tenant availability:
    /// custom roles must be made available to the tenant (via <see cref="TenantRole"/>); system roles
    /// are universal. Enum-driven assignments link to the matching seeded system role when present.
    /// </summary>
    private async Task<(Guid? RoleId, IActionResult? Error)> ResolveAssignmentRoleIdAsync(
        UserRole role, Role? roleEntity, Guid tenantId, CancellationToken cancellationToken)
    {
        if (roleEntity is not null)
        {
            if (!roleEntity.IsSystem && await _roles.GetTenantRoleAsync(tenantId, roleEntity.Id, cancellationToken) is null)
            {
                return (null, BadRequest(ApiResponseFactory.Error(
                    ApiErrorCodes.ValidationFailed, "Validation failed.", "Role is not available to this tenant.")));
            }

            return (roleEntity.Id, null);
        }

        var systemRole = await _roles.GetByNameAsync(role.ToString(), cancellationToken);
        return (systemRole?.Id, null);
    }

    /// <summary>
    /// Maps an RBAC role to a legacy fixed-tier enum for the transition period: system roles map by
    /// name; custom roles fall back to an explicit enum if given, otherwise least-privilege Operator
    /// (the enum is superseded by permission-based authorization in Phase 3).
    /// </summary>
    private static UserRole MapLegacyRole(Role roleEntity, string? explicitRole)
    {
        if (roleEntity.IsSystem && Enum.TryParse<UserRole>(roleEntity.Name, ignoreCase: false, out var system))
        {
            return system;
        }

        if (!string.IsNullOrWhiteSpace(explicitRole) && Enum.TryParse<UserRole>(explicitRole, ignoreCase: false, out var explicitEnum))
        {
            return explicitEnum;
        }

        return UserRole.Operator;
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

    private static UserDetail Map(User user)
    {
        var p = user.Person;
        return new UserDetail(
            user.Id,
            user.PersonId,
            user.Email,
            p?.FirstName ?? string.Empty,
            p?.LastName ?? string.Empty,
            p?.FullName ?? user.DisplayName,
            p?.MobileNumber,
            user.DisplayName,
            user.IsActive,
            user.MustChangePassword,
            user.TenantRoles.Select(r => new TenantAssignmentDto(r.TenantId, r.Role.ToString(), r.RoleId, r.RoleEntity?.Name)).ToList());
    }

    /// <summary>Generates a unique business person code (e.g. PER-AB12CD34EF).</summary>
    private async Task<string> GeneratePersonCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = "PER-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
            if (!await _persons.PersonCodeExistsAsync(code, cancellationToken))
            {
                return code;
            }
        }

        return "PER-" + Guid.NewGuid().ToString("N").ToUpperInvariant();
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveActorNamesAsync(IEnumerable<Guid?> ids, CancellationToken cancellationToken)
        => await _users.GetFullNamesAsync(ids.Where(id => id.HasValue).Select(id => id!.Value), cancellationToken);

    private static string? NameOf(IReadOnlyDictionary<Guid, string> names, Guid? id)
        => id.HasValue && names.TryGetValue(id.Value, out var name) ? name : null;
}
