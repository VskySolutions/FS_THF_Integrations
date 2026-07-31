using EmsPortal.Api.Models.Users;
using EmsPortal.Api.Security;
using EmsPortal.Application.Abstractions.Auditing;
using EmsPortal.Application.Abstractions.Email;
using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Application.Abstractions.Security;
using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;
using EmsPortal.Shared.Contracts;
using EmsPortal.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmsPortal.Api.Controllers;

/// <summary>
/// User account management (WO-38). Super Admins manage all users; Tenant Admins manage
/// Tenant Admins and custom-role users within their active tenant (REQ-ADM-001/002/003/009/010).
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
    private readonly ITenantRepository _tenants;
    private readonly IUserGroupRepository _groups;
    private readonly IPermissionGroupRepository _permissionGroups;
    private readonly IEmailNotificationService _emailNotifications;
    private readonly IEmailDispatcher _emailDispatcher;

    public UsersController(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IRefreshTokenRepository refreshTokens,
        IUnitOfWork unitOfWork,
        IAuditTrailService audit,
        IRoleRepository roles,
        IPersonRepository persons,
        ITenantRepository tenants,
        IUserGroupRepository groups,
        IPermissionGroupRepository permissionGroups,
        IEmailNotificationService emailNotifications,
        IEmailDispatcher emailDispatcher)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _refreshTokens = refreshTokens;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _roles = roles;
        _persons = persons;
        _tenants = tenants;
        _groups = groups;
        _permissionGroups = permissionGroups;
        _emailNotifications = emailNotifications;
        _emailDispatcher = emailDispatcher;
    }

    [HttpPost("/api/admin/users")]
    [RequirePermission(Permissions.UsersWrite)]
    [ProducesResponseType<ApiResponse<CreateUserResponse>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var (targetRoles, roleError) = await ResolveTargetRolesAsync(request.RoleIds, request.RoleId, request.Role, cancellationToken);
        if (roleError is not null)
        {
            return roleError;
        }
        if (targetRoles.Count == 0)
        {
            return BadRequest(ApiResponseFactory.Error(
                ApiErrorCodes.ValidationFailed, "Validation failed.", "At least one role is required."));
        }

        var anySuperAdmin = targetRoles.Any(IsSuperAdminRole);

        Guid? tenantId;

        if (User.IsSuperAdmin())
        {
            if (!anySuperAdmin && request.TenantId is null)
            {
                return BadRequest(ApiResponseFactory.Error(
                    ApiErrorCodes.ValidationFailed, "Validation failed.", "tenantId is required for tenant-scoped roles."));
            }

            tenantId = request.TenantId;
        }
        else
        {
            // Tenant Admin: may only create non-Super-Admin users in their own tenant.
            if (anySuperAdmin)
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

        // Capacity (WO-119): if any target role composes a capped group in the tenant, reject when adding
        // this new (active) user would push the group past its limit (AC-PG-013.2). Checked before any
        // persistence so a rejection never leaves a half-created account.
        var userId = Guid.NewGuid();
        if (tenantId is { } capacityTenant)
        {
            var capacityError = await CheckAssignmentCapacityAsync(
                userId, userIsActive: true, capacityTenant, targetRoles.Select(r => r.RoleId).ToList(), cancellationToken);
            if (capacityError is not null)
            {
                return capacityError;
            }
        }

        // A user is created by promoting an existing Person master record (WO-61). Super Admins may
        // promote a person from any tenant; Tenant Admins are restricted to their own by the tenant filter.
        var person = User.IsSuperAdmin()
            ? await _persons.GetByIdUnscopedAsync(request.PersonId, cancellationToken)
            : await _persons.GetByIdAsync(request.PersonId, cancellationToken);
        if (person is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Person not found."));
        }

        if (person.UserId is not null)
        {
            return Conflict(ApiResponseFactory.Error(
                ApiErrorCodes.DuplicateIdentifier, "Person is already a user.", person.PersonCode));
        }

        var email = string.IsNullOrWhiteSpace(request.Email) ? person.PrimaryEmail : request.Email;
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(ApiResponseFactory.Error(
                ApiErrorCodes.ValidationFailed, "Validation failed.", "An email is required (the person has none)."));
        }

        if (await _users.EmailExistsAsync(email, cancellationToken))
        {
            return Conflict(ApiResponseFactory.Error(
                ApiErrorCodes.DuplicateIdentifier, "Email already in use.", email));
        }

        var temporaryPassword = _passwordHasher.GenerateTemporaryPassword();
        var (hash, salt) = _passwordHasher.Hash(temporaryPassword);

        // Link the person to the new account and refresh its contact details from the request.
        person.UserId = userId;
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            person.MobileNumber = request.PhoneNumber;
        }
        if (!string.IsNullOrWhiteSpace(request.CountryCode))
        {
            person.CountryCode = request.CountryCode;
        }
        if (string.IsNullOrWhiteSpace(person.PrimaryEmail))
        {
            person.PrimaryEmail = email;
        }
        _persons.Update(person);

        var user = new User
        {
            Id = userId,
            Email = email,
            DisplayName = person.FullName,
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
            // One row per role — the user may hold several roles in the tenant (multi-role).
            foreach (var targetRole in targetRoles)
            {
                await _users.AddAssignmentAsync(new UserTenantRole
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    TenantId = tid,
                    Role = targetRole.LegacyRole,
                    RoleId = targetRole.RoleId,
                }, cancellationToken);
            }
        }

        await _audit.AddAsync(nameof(User), user.Id.ToString(), "Created",
            details: $"roles={string.Join(",", targetRoles.Select(r => r.Entity.Name))}; tenant={tenantId}", cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Optionally email the invitation (with the temporary password) via the tenant's active SMTP
        // account. The send runs in the background; the flag reflects whether it will be attempted (an
        // active SMTP account exists), so the caller knows whether to share the password manually.
        var invitationEmailSent = false;
        if (request.SendInvitation && tenantId is { } inviteTenant)
        {
            invitationEmailSent = await _emailNotifications.HasActiveSenderAsync(inviteTenant, cancellationToken);
            _emailDispatcher.Enqueue(inviteTenant, EmailTemplateKey.UserInvitation, user.Email,
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["FullName"] = user.DisplayName,
                    ["Email"] = user.Email,
                    ["TemporaryPassword"] = temporaryPassword,
                });
        }

        return StatusCode(StatusCodes.Status201Created,
            ApiResponseFactory.Success(new CreateUserResponse(user.Id, temporaryPassword, invitationEmailSent), "User created."));
    }

    [HttpGet("/api/admin/users")]
    [RequirePermission(Permissions.UsersRead)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string? search = null,
        [FromQuery] Guid? tenantId = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? name = null,
        [FromQuery] string? email = null,
        [FromQuery] string? phone = null,
        [FromQuery] string? role = null,
        [FromQuery] string? group = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);

        // Super Admins may filter by any tenant; everyone else is scoped to their active tenant.
        Guid? tenantFilter = User.IsSuperAdmin() ? tenantId : User.GetActiveTenantId();
        var (items, total) = await _users.ListAsync(tenantFilter, search, isActive, name, email, phone, role, group, page, limit, cancellationToken);

        var names = await ResolveActorNamesAsync(items.SelectMany(u => new[] { u.CreatedById, u.UpdatedById }), cancellationToken);

        // Resolve tenant names for the assignment(s) shown in the list.
        var allTenants = await _tenants.ListAsync(cancellationToken);
        var tenantNames = allTenants.ToDictionary(t => t.Id, t => t.Name);
        string? TenantNamesFor(User u)
        {
            var distinct = u.TenantRoles
                .Select(r => tenantNames.TryGetValue(r.TenantId, out var name) ? name : null)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .ToList();
            return distinct.Count == 0 ? null : string.Join(", ", distinct);
        }

        // The distinct role names held by the user (RBAC role name, falling back to the legacy tier).
        static IReadOnlyList<string> RolesFor(User u) => u.TenantRoles
            .Select(r => r.RoleEntity?.Name ?? r.Role.ToString())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        var summaries = items.Select(u => new UserSummary(
            u.Id, u.Email,
            u.Person?.FirstName ?? string.Empty, u.Person?.LastName ?? string.Empty,
            u.Person?.FullName ?? u.DisplayName, u.Person?.MobileNumber, TenantNamesFor(u), RolesFor(u), GroupsFor(u, tenantFilter), u.IsActive,
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

        var wasActive = user.IsActive;
        if (!request.IsActive && user.IsActive)
        {
            user.TokenVersion++; // deactivation invalidates sessions
        }

        user.IsActive = request.IsActive;
        _users.Update(user);
        await _audit.AddAsync(nameof(User), user.Id.ToString(),
            request.IsActive ? "Activated" : "Deactivated", cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Best-effort welcome email on the inactive → active transition.
        if (request.IsActive && !wasActive && TenantForUserEmail(user) is { } welcomeTenant)
        {
            _emailDispatcher.Enqueue(welcomeTenant, EmailTemplateKey.Welcome, user.Email,
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["FullName"] = user.DisplayName,
                });
        }

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

        // Email the new temporary password in the background via the relevant tenant's active SMTP
        // account; the flag reflects whether a send will be attempted (so the caller can share manually).
        var emailSent = false;
        if (TenantForUserEmail(user) is { } resetTenant)
        {
            emailSent = await _emailNotifications.HasActiveSenderAsync(resetTenant, cancellationToken);
            _emailDispatcher.Enqueue(resetTenant, EmailTemplateKey.PasswordReset, user.Email,
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["FullName"] = user.DisplayName,
                    ["TemporaryPassword"] = temporaryPassword,
                });
        }

        // AC-ADM-013.2: the plaintext temporary password is returned only in this response.
        return Ok(ApiResponseFactory.Success(
            new ResetPasswordResponse(user.Id, temporaryPassword, emailSent), "Password reset."));
    }

    [HttpPost("/api/admin/users/{id:guid}/tenant-assignments")]
    [RequirePermission(Permissions.RolesAssign)]
    public async Task<IActionResult> AssignTenantRole(Guid id, [FromBody] AssignTenantRoleRequest request, CancellationToken cancellationToken)
    {
        // Changing a user's role is restricted to Super Admins regardless of any granted permission.
        if (!User.IsSuperAdmin())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponseFactory.Forbidden("Only a Super Admin can change role assignments."));
        }

        var user = await _users.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound(ApiResponseFactory.NotFound("User not found."));
        }

        var (targetRoles, roleError) = await ResolveTargetRolesAsync(request.RoleIds, request.RoleId, request.Role, cancellationToken);
        if (roleError is not null)
        {
            return roleError;
        }
        if (targetRoles.Count == 0)
        {
            return BadRequest(ApiResponseFactory.Error(
                ApiErrorCodes.ValidationFailed, "Validation failed.", "At least one role is required."));
        }

        // Capacity (WO-119): only roles the user does not already hold in the tenant can add them to a new
        // group's population. Reject if granting one composes a full capped group and this user would be a
        // new distinct member beyond the limit (AC-PG-013.2).
        var currentAssignments = await _users.GetAssignmentsAsync(id, request.TenantId, cancellationToken);
        var currentRoleIds = currentAssignments.Select(a => a.RoleId).ToHashSet();
        var addedRoleIds = targetRoles.Select(r => r.RoleId).Where(rid => !currentRoleIds.Contains(rid)).ToList();
        var capacityError = await CheckAssignmentCapacityAsync(user.Id, user.IsActive, request.TenantId, addedRoleIds, cancellationToken);
        if (capacityError is not null)
        {
            return capacityError;
        }

        // Reconcile the tenant's role set to exactly the requested roles: add missing, soft-delete absent
        // (AC-ADM-006.2). The validator guarantees a non-empty set here.
        await ReconcileTenantRolesAsync(id, request.TenantId, targetRoles, cancellationToken);

        var roleNames = targetRoles.Select(r => r.Entity.Name).ToArray();
        await _audit.AddAsync(nameof(User), id.ToString(), "TenantRoleAssigned",
            details: $"tenant={request.TenantId}; roles={string.Join(",", roleNames)}", cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponseFactory.Success(new { userId = id, tenantId = request.TenantId, roleNames }, "Assignment saved."));
    }

    [HttpDelete("/api/admin/users/{id:guid}/tenant-assignments/{tenantId:guid}")]
    [RequirePermission(Permissions.RolesAssign)]
    public async Task<IActionResult> RemoveTenantRole(Guid id, Guid tenantId, CancellationToken cancellationToken)
    {
        // Removing a user's role assignment is restricted to Super Admins.
        if (!User.IsSuperAdmin())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponseFactory.Forbidden("Only a Super Admin can change role assignments."));
        }

        var existing = await _users.GetAssignmentsAsync(id, tenantId, cancellationToken);
        if (existing.Count == 0)
        {
            return NotFound(ApiResponseFactory.NotFound("Assignment not found."));
        }

        // Remove tenant access entirely: soft-delete every role the user holds in the tenant
        // (the resulting set is empty — AC-ADM-006.3).
        foreach (var assignment in existing)
        {
            _users.RemoveAssignment(assignment);
        }

        await _audit.AddAsync(nameof(User), id.ToString(), "TenantRoleRemoved",
            details: $"tenant={tenantId}", cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponseFactory.Success(new { message = "Assignment removed." }, "Assignment removed."));
    }

    // ---- User groups ----

    /// <summary>Replaces the user's group memberships (in the active tenant) with the supplied set.</summary>
    [HttpPut("/api/admin/users/{id:guid}/groups")]
    [RequirePermission(Permissions.UsersGroupManagement)]
    public async Task<IActionResult> SetGroups(Guid id, [FromBody] AssignUserGroupsRequest request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(id, cancellationToken);
        if (user is null || !CanCallerSee(user))
        {
            return NotFound(ApiResponseFactory.NotFound("User not found."));
        }

        var requested = (request.GroupIds ?? new List<Guid>()).Distinct().ToHashSet();

        // Every requested group must exist in the active tenant (the repo is tenant-scoped).
        foreach (var groupId in requested)
        {
            if (await _groups.GetByIdAsync(groupId, cancellationToken) is null)
            {
                return BadRequest(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed, "Validation failed.", $"Unknown group {groupId}."));
            }
        }

        var existing = await _groups.GetMembershipsForUserAsync(id, cancellationToken);
        var existingIds = existing.Select(m => m.UserGroupId).ToHashSet();

        foreach (var membership in existing.Where(m => !requested.Contains(m.UserGroupId)))
        {
            _groups.RemoveMember(membership);
        }
        foreach (var groupId in requested.Where(g => !existingIds.Contains(g)))
        {
            await _groups.AddMemberAsync(new UserGroupMember { Id = Guid.NewGuid(), UserGroupId = groupId, UserId = id }, cancellationToken);
        }

        await _audit.AddAsync(nameof(User), id.ToString(), "GroupsUpdated", cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponseFactory.Success(new { userId = id, groupIds = requested.ToList() }, "Groups updated."));
    }

    /// <summary>A concrete role to assign: its id, the loaded RBAC role, and its legacy fixed-tier shadow.</summary>
    private sealed record ResolvedRole(Guid RoleId, Role Entity, UserRole LegacyRole);

    private static bool IsSuperAdminRole(ResolvedRole role)
        => string.Equals(role.Entity.Name, Roles.SuperAdmin, StringComparison.Ordinal);

    /// <summary>
    /// Resolves an assignment request into the concrete set of roles to reconcile: the multi-role
    /// <paramref name="roleIds"/> plus the legacy single <paramref name="legacyRoleId"/>; or — only when
    /// no ids are supplied — the legacy <paramref name="legacyRoleName"/> enum mapped to its seeded system
    /// role (back-compat). Every id must resolve to a known role. Duplicates collapse to one.
    /// </summary>
    private async Task<(IReadOnlyList<ResolvedRole> Roles, IActionResult? Error)> ResolveTargetRolesAsync(
        IEnumerable<Guid> roleIds, Guid? legacyRoleId, string? legacyRoleName, CancellationToken cancellationToken)
    {
        var ids = roleIds?.ToList() ?? new List<Guid>();
        if (legacyRoleId is { } single)
        {
            ids.Add(single);
        }

        var resolved = new List<ResolvedRole>();
        foreach (var roleId in ids.Distinct())
        {
            var entity = await _roles.GetByIdAsync(roleId, cancellationToken);
            if (entity is null)
            {
                return (Array.Empty<ResolvedRole>(), BadRequest(ApiResponseFactory.Error(
                    ApiErrorCodes.ValidationFailed, "Validation failed.", $"Unknown roleId {roleId}.")));
            }

            resolved.Add(new ResolvedRole(entity.Id, entity, MapLegacyRole(entity, null)));
        }

        // Legacy single-role-by-name fallback (only when no ids were supplied).
        if (resolved.Count == 0 && !string.IsNullOrWhiteSpace(legacyRoleName))
        {
            if (!Enum.TryParse<UserRole>(legacyRoleName, ignoreCase: false, out var enumValue))
            {
                return (Array.Empty<ResolvedRole>(), BadRequest(ApiResponseFactory.Error(
                    ApiErrorCodes.ValidationFailed, "Validation failed.", "A valid role or roleId is required.")));
            }

            var entity = await _roles.GetByNameAsync(legacyRoleName, cancellationToken);
            if (entity is null)
            {
                return (Array.Empty<ResolvedRole>(), BadRequest(ApiResponseFactory.Error(
                    ApiErrorCodes.ValidationFailed, "Validation failed.", "No RBAC role matches the requested role.")));
            }

            resolved.Add(new ResolvedRole(entity.Id, entity, enumValue));
        }

        return (resolved, null);
    }

    /// <summary>
    /// Reconciles the user's active role assignments in a tenant to exactly <paramref name="target"/>:
    /// missing roles are added, roles no longer present are soft-deleted. An empty target removes the
    /// user's tenant access entirely (AC-ADM-006.3).
    /// </summary>
    private async Task ReconcileTenantRolesAsync(
        Guid userId, Guid tenantId, IReadOnlyList<ResolvedRole> target, CancellationToken cancellationToken)
    {
        var existing = await _users.GetAssignmentsAsync(userId, tenantId, cancellationToken);
        var targetIds = target.Select(t => t.RoleId).ToHashSet();
        var existingIds = existing.Select(e => e.RoleId).ToHashSet();

        // Soft-delete assignments no longer in the target set.
        foreach (var assignment in existing.Where(e => !targetIds.Contains(e.RoleId)))
        {
            _users.RemoveAssignment(assignment);
        }

        // Add roles the user does not yet hold in the tenant.
        foreach (var role in target.Where(t => !existingIds.Contains(t.RoleId)))
        {
            await _users.AddAssignmentAsync(new UserTenantRole
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TenantId = tenantId,
                Role = role.LegacyRole,
                RoleId = role.RoleId,
            }, cancellationToken);
        }
    }

    /// <summary>
    /// Enforces Permission Group capacity limits (WO-119) when <paramref name="addedRoleIds"/> are about to
    /// be granted to a user in <paramref name="tenantId"/>. For each capped group in that tenant composed by
    /// a newly-granted role, if the user is not already an active member and admitting them would push usage
    /// past the limit, a limit-reached error is returned (and the rejection audited); otherwise null.
    /// </summary>
    private async Task<IActionResult?> CheckAssignmentCapacityAsync(
        Guid userId, bool userIsActive, Guid tenantId, IReadOnlyList<Guid> addedRoleIds, CancellationToken cancellationToken)
    {
        // Inactive users never count toward usage, and no new roles means no growth.
        if (!userIsActive || addedRoleIds.Count == 0)
        {
            return null;
        }

        var groups = await _permissionGroups.GetGroupsByRolesAsync(addedRoleIds, cancellationToken);
        foreach (var group in groups.Where(g => g.TenantId == tenantId && g.CapacityLimit.HasValue))
        {
            var limit = group.CapacityLimit!.Value;

            // Already a member (via a role they keep) → not a new distinct user → no growth.
            if (await _permissionGroups.IsUserActiveMemberAsync(group.Id, tenantId, userId, cancellationToken))
            {
                continue;
            }

            var projected = await _permissionGroups.CountActiveMembersAsync(group.Id, tenantId, null, cancellationToken) + 1;
            if (projected > limit)
            {
                await _audit.AddAsync(nameof(PermissionGroup), group.Id.ToString(), "CapacityLimitReached",
                    details: $"Assigning a role composing '{group.Name}' to user {userId} would raise usage to {projected}, above the limit of {limit}.",
                    cancellationToken: cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return BadRequest(ApiResponseFactory.Error(
                    ApiErrorCodes.CapacityLimitReached,
                    $"Cannot assign this role: permission group '{group.Name}' is at its capacity limit ({limit}).",
                    group.Name));
            }
        }

        return null;
    }

    /// <summary>
    /// Maps an RBAC role to a legacy fixed-tier enum for the transition period: system roles map by
    /// name; custom roles fall back to an explicit enum if given, otherwise the neutral
    /// <see cref="UserRole.Custom"/> sentinel (the enum is superseded by permission-based authorization).
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

        return UserRole.Custom;
    }

    /// <summary>The tenant whose SMTP account should send a user's email: the caller's active tenant, else the user's first assignment.</summary>
    private Guid? TenantForUserEmail(User user)
        => User.GetActiveTenantId() ?? user.TenantRoles.FirstOrDefault()?.TenantId;

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
            // Group the (multi-role) assignments by tenant → one row carrying all roles held there.
            user.TenantRoles
                .GroupBy(r => r.TenantId)
                .Select(g => new TenantAssignmentDto(
                    g.Key,
                    g.Select(r => new TenantAssignmentRoleDto(r.RoleId, r.RoleEntity?.Name, r.Role.ToString())).ToList()))
                .ToList(),
            // GetByIdAsync already scopes memberships to the active tenant via the ambient filter.
            GroupsFor(user, null));
    }

    /// <summary>The user's group memberships as DTOs, optionally restricted to a specific tenant.</summary>
    private static IReadOnlyList<UserGroupDto> GroupsFor(User user, Guid? tenantId) => user.GroupMemberships
        .Where(m => !m.Deleted && m.UserGroup != null && (tenantId == null || m.TenantId == tenantId))
        .Select(m => new UserGroupDto(m.UserGroup!.Id, m.UserGroup!.Name))
        .OrderBy(g => g.Name)
        .ToList();

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveActorNamesAsync(IEnumerable<Guid?> ids, CancellationToken cancellationToken)
        => await _users.GetFullNamesAsync(ids.Where(id => id.HasValue).Select(id => id!.Value), cancellationToken);

    private static string? NameOf(IReadOnlyDictionary<Guid, string> names, Guid? id)
        => id.HasValue && names.TryGetValue(id.Value, out var name) ? name : null;
}
