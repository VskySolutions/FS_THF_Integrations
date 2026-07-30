using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmsPortal.Infrastructure.Persistence.Repositories;

internal sealed class RemsClientRepository : IRemsClientRepository
{
    private readonly EmsPortalDbContext _dbContext;

    public RemsClientRepository(EmsPortalDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<REMSClient?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => LoadGraph().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<REMSClient?> GetByRemsIdAsync(Guid remsId, CancellationToken cancellationToken = default)
        => LoadGraph().FirstOrDefaultAsync(c => c.REMSId == remsId, cancellationToken);

    public async Task AddAsync(REMSClient client, CancellationToken cancellationToken = default)
        => await _dbContext.RemsClients.AddAsync(client, cancellationToken);

    public void Update(REMSClient client) => _dbContext.RemsClients.Update(client);

    public void Remove(REMSClient client) => _dbContext.RemsClients.Remove(client);

    public async Task AddEntityAsync(REMSEntity entity, CancellationToken cancellationToken = default)
        => await _dbContext.RemsEntities.AddAsync(entity, cancellationToken);

    public void RemoveEntity(REMSEntity entity) => _dbContext.RemsEntities.Remove(entity);

    public async Task AddEntityAddressAsync(REMSEntityAddress address, CancellationToken cancellationToken = default)
        => await _dbContext.RemsEntityAddresses.AddAsync(address, cancellationToken);

    public async Task AddEntityContactAsync(REMSEntityContact contact, CancellationToken cancellationToken = default)
        => await _dbContext.RemsEntityContacts.AddAsync(contact, cancellationToken);

    private IQueryable<REMSClient> LoadGraph()
        => _dbContext.RemsClients
            .Include(c => c.Entities).ThenInclude(e => e.Addresses)
            .Include(c => c.Entities).ThenInclude(e => e.Contacts);
}
