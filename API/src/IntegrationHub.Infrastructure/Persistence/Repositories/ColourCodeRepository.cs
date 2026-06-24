using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence.Repositories;

internal sealed class ColourCodeRepository : IColourCodeRepository
{
    private readonly IntegrationHubDbContext _dbContext;

    public ColourCodeRepository(IntegrationHubDbContext dbContext) => _dbContext = dbContext;

    public Task AddAsync(ColourCode colourCode, CancellationToken cancellationToken = default)
        => _dbContext.ColourCodes.AddAsync(colourCode, cancellationToken).AsTask();

    public Task<ColourCode?> GetAsync(Guid userId, EntityType entityType, Guid entityId, CancellationToken cancellationToken = default)
        => _dbContext.ColourCodes.FirstOrDefaultAsync(
            c => c.UserId == userId && c.EntityType == entityType && c.EntityId == entityId, cancellationToken);

    public void Update(ColourCode colourCode) => _dbContext.ColourCodes.Update(colourCode);

    public void Remove(ColourCode colourCode) => _dbContext.ColourCodes.Remove(colourCode);

    public async Task<IReadOnlyDictionary<Guid, string>> GetBatchAsync(
        Guid userId, EntityType entityType, IReadOnlyCollection<Guid> entityIds, CancellationToken cancellationToken = default)
    {
        if (entityIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await _dbContext.ColourCodes
            .Where(c => c.UserId == userId && c.EntityType == entityType && entityIds.Contains(c.EntityId))
            .ToDictionaryAsync(c => c.EntityId, c => c.Colour, cancellationToken);
    }
}
