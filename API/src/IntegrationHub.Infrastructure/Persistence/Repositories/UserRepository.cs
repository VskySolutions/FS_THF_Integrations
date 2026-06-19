using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository : IUserRepository
{
    private readonly IntegrationHubDbContext _dbContext;

    public UserRepository(IntegrationHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.Include(u => u.TenantRoles).ThenInclude(r => r.RoleEntity)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        await LoadPersonAsync(user, cancellationToken);
        return user;
    }

    // Person carries a tenant query filter, which EF would also apply to an Include — hiding a user's
    // linked profile (and blanking their name) whenever the person's tenant differs from the active
    // one or is unset. Load it explicitly with the filter ignored; user-level tenant scoping is
    // enforced by the controller, so this only ever surfaces the user's own profile.
    private async Task LoadPersonAsync(User? user, CancellationToken cancellationToken)
    {
        if (user?.PersonId is { } personId)
        {
            user.Person = await _dbContext.Persons.IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == personId && !p.Deleted, cancellationToken);
        }
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => _dbContext.Users.Include(u => u.TenantRoles).ThenInclude(r => r.RoleEntity)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
        => _dbContext.Users.AnyAsync(u => u.Email == email, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> GetFullNamesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        // Ignore filters so soft-deleted creators still resolve to a name. Names now live on
        // the associated Person; fall back to the user's display name, then email.
        var users = await _dbContext.Users.IgnoreQueryFilters()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                u.DisplayName,
                u.Email,
                FirstName = u.Person != null ? u.Person.FirstName : null,
                LastName = u.Person != null ? u.Person.LastName : null
            })
            .ToListAsync(cancellationToken);

        return users.ToDictionary(
            u => u.Id,
            u =>
            {
                var name = string.Join(" ", new[] { u.FirstName, u.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
                return string.IsNullOrWhiteSpace(name)
                    ? (string.IsNullOrWhiteSpace(u.DisplayName) ? u.Email : u.DisplayName)
                    : name;
            });
    }

    public async Task<(IReadOnlyList<User> Items, int Total)> ListAsync(
        Guid? tenantId, string? search, bool? isActive,
        string? name, string? email, string? phone, string? role,
        int page, int limit, CancellationToken cancellationToken = default)
    {
        // Ignore query filters (the Person tenant filter would otherwise blank the name / drop the row
        // for users whose person tenant differs or is unset) and re-apply the soft-delete predicates.
        var query = _dbContext.Users
            .IgnoreQueryFilters()
            .Where(u => !u.Deleted)
            .Include(u => u.TenantRoles.Where(r => !r.Deleted)).ThenInclude(r => r.RoleEntity)
            .Include(u => u.Person)
            .AsQueryable();
        if (tenantId is { } id)
        {
            query = query.Where(u => u.TenantRoles.Any(r => r.TenantId == id && !r.Deleted));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u =>
                u.Email.Contains(term) ||
                (u.Person != null && (u.Person.FirstName.Contains(term) || u.Person.LastName.Contains(term))));
        }
        if (isActive is { } active)
        {
            query = query.Where(u => u.IsActive == active);
        }
        // Per-column "contains" filters (paired with the list's per-column filter drawer).
        if (!string.IsNullOrWhiteSpace(name))
        {
            var t = name.Trim();
            query = query.Where(u => u.Person != null &&
                (u.Person.FirstName.Contains(t) || u.Person.LastName.Contains(t) || u.Person.DisplayName.Contains(t)));
        }
        if (!string.IsNullOrWhiteSpace(email))
        {
            var t = email.Trim();
            query = query.Where(u => u.Email.Contains(t));
        }
        if (!string.IsNullOrWhiteSpace(phone))
        {
            var t = phone.Trim();
            query = query.Where(u => u.Person != null && u.Person.MobileNumber != null && u.Person.MobileNumber.Contains(t));
        }
        if (!string.IsNullOrWhiteSpace(role))
        {
            var t = role.Trim();
            query = query.Where(u => u.TenantRoles.Any(r => !r.Deleted && r.RoleEntity != null && r.RoleEntity.Name.Contains(t)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(u => u.UpdatedOnUtc)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
        => await _dbContext.Users.AddAsync(user, cancellationToken);

    public void Update(User user) => _dbContext.Users.Update(user);

    public Task<UserTenantRole?> GetAssignmentAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
        => _dbContext.UserTenantRoles.FirstOrDefaultAsync(
            r => r.UserId == userId && r.TenantId == tenantId, cancellationToken);

    public async Task AddAssignmentAsync(UserTenantRole assignment, CancellationToken cancellationToken = default)
        => await _dbContext.UserTenantRoles.AddAsync(assignment, cancellationToken);

    public void RemoveAssignment(UserTenantRole assignment) => _dbContext.UserTenantRoles.Remove(assignment);
}
