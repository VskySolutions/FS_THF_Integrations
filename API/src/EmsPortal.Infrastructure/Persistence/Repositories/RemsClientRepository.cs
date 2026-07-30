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

    public Task<REMSEntity?> GetEntityAsync(Guid entityId, CancellationToken cancellationToken = default)
        => _dbContext.RemsEntities
            .Include(e => e.Client)
            .Include(e => e.Addresses).ThenInclude(a => a.Address)
            .Include(e => e.Contacts).ThenInclude(c => c.Person)
            .FirstOrDefaultAsync(e => e.Id == entityId, cancellationToken);

    public async Task AddEntityAsync(REMSEntity entity, CancellationToken cancellationToken = default)
        => await _dbContext.RemsEntities.AddAsync(entity, cancellationToken);

    public void RemoveEntity(REMSEntity entity) => _dbContext.RemsEntities.Remove(entity);

    public async Task AddEntityAddressAsync(REMSEntityAddress address, CancellationToken cancellationToken = default)
        => await _dbContext.RemsEntityAddresses.AddAsync(address, cancellationToken);

    public void RemoveEntityAddress(REMSEntityAddress address) => _dbContext.RemsEntityAddresses.Remove(address);

    public async Task AddEntityContactAsync(REMSEntityContact contact, CancellationToken cancellationToken = default)
        => await _dbContext.RemsEntityContacts.AddAsync(contact, cancellationToken);

    public void RemoveEntityContact(REMSEntityContact contact) => _dbContext.RemsEntityContacts.Remove(contact);

    private IQueryable<REMSClient> LoadGraph()
        => _dbContext.RemsClients
            .Include(c => c.BillingAddress)
            .Include(c => c.Entities).ThenInclude(e => e.Addresses).ThenInclude(a => a.Address)
            .Include(c => c.Entities).ThenInclude(e => e.Contacts).ThenInclude(ct => ct.Person);
}
