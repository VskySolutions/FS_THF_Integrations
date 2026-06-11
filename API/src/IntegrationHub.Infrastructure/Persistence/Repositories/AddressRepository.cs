using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence.Repositories;

internal sealed class AddressRepository : IAddressRepository
{
    private readonly IntegrationHubDbContext _dbContext;

    public AddressRepository(IntegrationHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Address?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Addresses.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task AddAsync(Address address, CancellationToken cancellationToken = default)
        => await _dbContext.Addresses.AddAsync(address, cancellationToken);

    public void Update(Address address) => _dbContext.Addresses.Update(address);
}
