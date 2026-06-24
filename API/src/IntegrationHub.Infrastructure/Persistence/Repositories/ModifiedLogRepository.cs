using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence.Repositories;

internal sealed class ModifiedLogRepository : IModifiedLogRepository
{
    private readonly IntegrationHubDbContext _dbContext;

    public ModifiedLogRepository(IntegrationHubDbContext dbContext) => _dbContext = dbContext;

    public async Task<(IReadOnlyList<FieldModifiedLog> Items, int Total)> ListAsync(
        EntityType entityType, Guid entityId, string fieldName, int page, int limit, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.FieldModifiedLogs
            .Where(l => l.EntityType == entityType && l.EntityId == entityId && l.FieldName == fieldName)
            .OrderByDescending(l => l.ChangedOnUtc);

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * limit).Take(limit).ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<IReadOnlyDictionary<string, int>> GetIconCountsAsync(EntityType entityType, Guid entityId, CancellationToken cancellationToken = default)
        => await _dbContext.FieldModifiedLogs
            .Where(l => l.EntityType == entityType && l.EntityId == entityId)
            .GroupBy(l => l.FieldName)
            .Select(g => new { FieldName = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.FieldName, x => x.Count, cancellationToken);

    public async Task<IReadOnlyList<ModifiedLogFieldConfig>> GetConfigsAsync(CancellationToken cancellationToken = default)
        => await _dbContext.ModifiedLogFieldConfigs.ToListAsync(cancellationToken);

    public Task<ModifiedLogFieldConfig?> GetConfigAsync(EntityType entityType, string fieldName, CancellationToken cancellationToken = default)
        => _dbContext.ModifiedLogFieldConfigs.FirstOrDefaultAsync(c => c.EntityType == entityType && c.FieldName == fieldName, cancellationToken);

    public Task AddConfigAsync(ModifiedLogFieldConfig config, CancellationToken cancellationToken = default)
        => _dbContext.ModifiedLogFieldConfigs.AddAsync(config, cancellationToken).AsTask();

    public void UpdateConfig(ModifiedLogFieldConfig config) => _dbContext.ModifiedLogFieldConfigs.Update(config);
}
