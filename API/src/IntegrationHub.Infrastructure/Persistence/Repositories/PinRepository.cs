using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence.Repositories;

internal sealed class PinRepository : IPinRepository
{
    private readonly IntegrationHubDbContext _dbContext;

    public PinRepository(IntegrationHubDbContext dbContext) => _dbContext = dbContext;

    public Task AddAsync(Pin pin, CancellationToken cancellationToken = default)
        => _dbContext.Pins.AddAsync(pin, cancellationToken).AsTask();

    public Task<Pin?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
        => _dbContext.Pins.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, cancellationToken);

    public Task<Pin?> GetAsync(Guid userId, EntityType entityType, Guid entityId, CancellationToken cancellationToken = default)
        => _dbContext.Pins.FirstOrDefaultAsync(
            p => p.UserId == userId && p.EntityType == entityType && p.EntityId == entityId, cancellationToken);

    public void Remove(Pin pin) => _dbContext.Pins.Remove(pin);

    public Task<int> CountByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => _dbContext.Pins.CountAsync(p => p.UserId == userId, cancellationToken);

    public Task<int> CountByUserAndTypeAsync(Guid userId, EntityType entityType, CancellationToken cancellationToken = default)
        => _dbContext.Pins.CountAsync(p => p.UserId == userId && p.EntityType == entityType, cancellationToken);

    public async Task<IReadOnlyList<Guid>> ListEntityIdsByUserAndTypeAsync(Guid userId, EntityType entityType, CancellationToken cancellationToken = default)
        => await _dbContext.Pins
            .Where(p => p.UserId == userId && p.EntityType == entityType)
            .Select(p => p.EntityId)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<Pin> Items, int Total)> ListByUserAsync(Guid userId, int page, int limit, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Pins.Where(p => p.UserId == userId).OrderByDescending(p => p.PinnedOnUtc);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * limit).Take(limit).ToListAsync(cancellationToken);
        return (items, total);
    }
}
