using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence.Repositories;

internal sealed class PersonRepository : IPersonRepository
{
    private readonly IntegrationHubDbContext _dbContext;

    public PersonRepository(IntegrationHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Person?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Persons
            .Include(p => p.Address)
            .Include(p => p.ProfileMedia)
            .Include(p => p.Tenant)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<Person?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => _dbContext.Persons
            .Include(p => p.Address)
            .Include(p => p.ProfileMedia)
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

    public Task<bool> PersonCodeExistsAsync(string personCode, CancellationToken cancellationToken = default)
        => _dbContext.Persons.AnyAsync(p => p.PersonCode == personCode, cancellationToken);

    public async Task<(IReadOnlyList<Person> Items, int Total)> ListAsync(
        string? search, int page, int limit, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Persons.Include(p => p.Tenant).AsQueryable();

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
