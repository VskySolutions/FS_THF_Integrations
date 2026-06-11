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
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<Person?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => _dbContext.Persons
            .Include(p => p.Address)
            .Include(p => p.ProfileMedia)
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

    public Task<bool> PersonCodeExistsAsync(string personCode, CancellationToken cancellationToken = default)
        => _dbContext.Persons.AnyAsync(p => p.PersonCode == personCode, cancellationToken);

    public async Task AddAsync(Person person, CancellationToken cancellationToken = default)
        => await _dbContext.Persons.AddAsync(person, cancellationToken);

    public void Update(Person person) => _dbContext.Persons.Update(person);
}
