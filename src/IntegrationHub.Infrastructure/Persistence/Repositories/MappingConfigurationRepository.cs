using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
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

    public async Task<IReadOnlyList<MappingConfiguration>> ListAsync(CancellationToken cancellationToken = default)
        => await _dbContext.MappingConfigurations
            .OrderBy(m => m.InterfaceName)
            .ThenByDescending(m => m.Version)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(MappingConfiguration configuration, CancellationToken cancellationToken = default)
        => await _dbContext.MappingConfigurations.AddAsync(configuration, cancellationToken);

    public void Update(MappingConfiguration configuration)
        => _dbContext.MappingConfigurations.Update(configuration);
}
