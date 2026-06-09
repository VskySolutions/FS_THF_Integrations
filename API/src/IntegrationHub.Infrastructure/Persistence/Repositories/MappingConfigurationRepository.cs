using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence.Repositories;

internal sealed class MappingConfigurationRepository : IMappingConfigurationRepository
{
    private readonly IntegrationHubDbContext _dbContext;

    public MappingConfigurationRepository(IntegrationHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<MappingConfiguration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.MappingConfigurations.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public Task<MappingConfiguration?> GetActiveAsync(string interfaceName, CancellationToken cancellationToken = default)
        => _dbContext.MappingConfigurations
            .Where(m => m.InterfaceName == interfaceName && m.IsActive)
            .OrderByDescending(m => m.Version)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<MappingConfiguration>> GetActiveByPairAsync(
        SystemName sourceSystem,
        SystemName destinationSystem,
        CancellationToken cancellationToken = default)
        => await _dbContext.MappingConfigurations
            .Where(m => m.IsActive && m.SourceSystem == sourceSystem && m.TargetSystem == destinationSystem)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MappingConfiguration>> ListAsync(CancellationToken cancellationToken = default)
        => await _dbContext.MappingConfigurations
            .OrderBy(m => m.InterfaceName)
            .ThenByDescending(m => m.Version)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<MappingConfiguration> Items, int Total)> ListByTenantAsync(
        Guid tenantId, int page, int limit, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.MappingConfigurations.IgnoreQueryFilters().Where(m => m.TenantId == tenantId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(m => m.UpdatedOnUtc)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<MappingConfiguration?> GetByIdForTenantAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
        => _dbContext.MappingConfigurations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == tenantId, cancellationToken);

    public Task<MappingConfiguration?> GetActiveForFieldAsync(
        Guid tenantId, SystemName sourceSystem, SystemName destinationSystem, string sourceField, CancellationToken cancellationToken = default)
        => _dbContext.MappingConfigurations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId
                && m.IsActive
                && m.SourceSystem == sourceSystem
                && m.TargetSystem == destinationSystem
                && m.SourceField == sourceField, cancellationToken);

    public async Task AddAsync(MappingConfiguration configuration, CancellationToken cancellationToken = default)
        => await _dbContext.MappingConfigurations.AddAsync(configuration, cancellationToken);

    public void Update(MappingConfiguration configuration)
        => _dbContext.MappingConfigurations.Update(configuration);

    public void Remove(MappingConfiguration configuration)
        => _dbContext.MappingConfigurations.Remove(configuration);
}
