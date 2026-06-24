using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Tenancy;
using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence.Repositories;

internal sealed class RetentionConfigRepository : IRetentionConfigRepository
{
    private readonly IntegrationHubDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public RetentionConfigRepository(IntegrationHubDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public Task<DeletedRecordRetentionConfig?> GetAsync(Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var effective = tenantId ?? (_tenantContext.IsResolved ? _tenantContext.TenantId : (Guid?)null);
        return effective is { } tid
            ? _dbContext.DeletedRecordRetentionConfigs.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.TenantId == tid && !c.Deleted, cancellationToken)
            : _dbContext.DeletedRecordRetentionConfigs.FirstOrDefaultAsync(cancellationToken);
    }

    public Task AddAsync(DeletedRecordRetentionConfig config, CancellationToken cancellationToken = default)
        => _dbContext.DeletedRecordRetentionConfigs.AddAsync(config, cancellationToken).AsTask();

    public void Update(DeletedRecordRetentionConfig config) => _dbContext.DeletedRecordRetentionConfigs.Update(config);
}
