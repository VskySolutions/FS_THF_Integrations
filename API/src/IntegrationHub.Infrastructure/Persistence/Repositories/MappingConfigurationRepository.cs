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

    public async Task<IReadOnlyList<MappingConfiguration>> GetActiveForFlowAsync(
        SystemName sourceSystem, SystemName destinationSystem, string interfaceName, CancellationToken cancellationToken = default)
        => await _dbContext.MappingConfigurations
            .Where(m => m.IsActive
                && m.SourceSystem == sourceSystem
                && m.TargetSystem == destinationSystem
                && m.InterfaceName == interfaceName)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MappingConfiguration>> ListByTenantFlowAsync(
        Guid tenantId, string interfaceName, CancellationToken cancellationToken = default)
        => await _dbContext.MappingConfigurations.IgnoreQueryFilters()
            .Where(m => !m.Deleted && m.TenantId == tenantId && m.InterfaceName == interfaceName)
            .OrderBy(m => m.SourceField)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(MappingConfiguration configuration, CancellationToken cancellationToken = default)
        => await _dbContext.MappingConfigurations.AddAsync(configuration, cancellationToken);

    public void Update(MappingConfiguration configuration)
        => _dbContext.MappingConfigurations.Update(configuration);

    public void Remove(MappingConfiguration configuration)
        => _dbContext.MappingConfigurations.Remove(configuration);
}
