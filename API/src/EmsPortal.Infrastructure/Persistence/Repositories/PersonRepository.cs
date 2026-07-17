using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmsPortal.Infrastructure.Persistence.Repositories;

internal sealed class PersonRepository : IPersonRepository
{
    private readonly EmsPortalDbContext _dbContext;

    public PersonRepository(EmsPortalDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Person?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Persons
            .Include(p => p.Address)
            .Include(p => p.ProfileMedia)
            .Include(p => p.Tenant)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    /// <summary>Super Admin (cross-tenant) read of a single person, bypassing the ambient tenant filter.</summary>
    public Task<Person?> GetByIdUnscopedAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Persons
            .IgnoreQueryFilters()
            .Include(p => p.Address)
            .Include(p => p.ProfileMedia)
            .Include(p => p.Tenant)
            .FirstOrDefaultAsync(p => p.Id == id && !p.Deleted, cancellationToken);

    // Self-profile lookup is keyed by the authenticated user's own id, so it bypasses the tenant
    // filter: a user's person may be stamped to a tenant other than the one currently active.
    public Task<Person?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => _dbContext.Persons
            .IgnoreQueryFilters()
            .Include(p => p.Address)
            .Include(p => p.ProfileMedia)
            .FirstOrDefaultAsync(p => p.UserId == userId && !p.Deleted, cancellationToken);

    public Task<bool> PersonCodeExistsAsync(string personCode, CancellationToken cancellationToken = default)
        => _dbContext.Persons.AnyAsync(p => p.PersonCode == personCode, cancellationToken);

    public async Task<(IReadOnlyList<Person> Items, int Total)> ListAsync(
        string? search, Guid? tenantId, bool? isUser, bool? isActive, int page, int limit, CancellationToken cancellationToken = default)
    {
        // Cross-tenant (Super Admin) reads pass an explicit tenant id and bypass the ambient filter;
        // everyone else gets the ambient-filtered set, pinned to their active tenant.
        var query = (tenantId is { } tid
            ? _dbContext.Persons.IgnoreQueryFilters().Where(p => p.TenantId == tid && !p.Deleted)
            : _dbContext.Persons.AsQueryable())
            .Include(p => p.Tenant)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p =>
                p.FirstName.Contains(term) ||
                p.LastName.Contains(term) ||
                p.DisplayName.Contains(term) ||
                p.PersonCode.Contains(term) ||
                (p.PrimaryEmail != null && p.PrimaryEmail.Contains(term)) ||
                (p.MobileNumber != null && p.MobileNumber.Contains(term)));
        }

        if (isUser is { } user)
        {
            query = user ? query.Where(p => p.UserId != null) : query.Where(p => p.UserId == null);
        }
        if (isActive is { } active)
        {
            query = query.Where(p => p.IsActive == active);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(p => p.UpdatedOnUtc)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<IReadOnlyList<(Person Person, bool IsUser)>> ListSelectableAsync(CancellationToken cancellationToken = default)
    {
        var items = await _dbContext.Persons
            .OrderBy(p => p.FirstName).ThenBy(p => p.LastName)
            .Select(p => new { Person = p, IsUser = p.UserId != null })
            .ToListAsync(cancellationToken);

        return items.Select(x => (x.Person, x.IsUser)).ToList();
    }

    public async Task AddAsync(Person person, CancellationToken cancellationToken = default)
        => await _dbContext.Persons.AddAsync(person, cancellationToken);

    public void Update(Person person) => _dbContext.Persons.Update(person);

    public void Remove(Person person) => _dbContext.Persons.Remove(person);
}
