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

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Users.Include(u => u.TenantRoles).FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => _dbContext.Users.Include(u => u.TenantRoles)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
        => _dbContext.Users.AnyAsync(u => u.Email == email, cancellationToken);

    public async Task<(IReadOnlyList<User> Items, int Total)> ListAsync(
        Guid? tenantId, int page, int limit, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Users.Include(u => u.TenantRoles).AsQueryable();
        if (tenantId is { } id)
        {
            query = query.Where(u => u.TenantRoles.Any(r => r.TenantId == id));
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
