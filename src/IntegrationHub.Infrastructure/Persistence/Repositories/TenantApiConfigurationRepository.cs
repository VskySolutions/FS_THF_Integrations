using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence.Repositories;

internal sealed class TenantApiConfigurationRepository : ITenantApiConfigurationRepository
{
    private readonly IntegrationHubDbContext _dbContext;

    public TenantApiConfigurationRepository(IntegrationHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<TenantApiConfiguration?> GetAsync(Guid tenantId, SystemName system, CancellationToken cancellationToken = default)
        => _dbContext.TenantApiConfigurations
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.System == system, cancellationToken);

    public async Task<IReadOnlyList<TenantApiConfiguration>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await _dbContext.TenantApiConfigurations
            .Where(c => c.TenantId == tenantId)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(TenantApiConfiguration configuration, CancellationToken cancellationToken = default)
        => await _dbContext.TenantApiConfigurations.AddAsync(configuration, cancellationToken);

    public void Update(TenantApiConfiguration configuration)
        => _dbContext.TenantApiConfigurations.Update(configuration);

    public void Remove(TenantApiConfiguration configuration)
        => _dbContext.TenantApiConfigurations.Remove(configuration);
}
