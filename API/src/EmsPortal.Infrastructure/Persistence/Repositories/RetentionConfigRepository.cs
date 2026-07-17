using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Application.Abstractions.Tenancy;
using EmsPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmsPortal.Infrastructure.Persistence.Repositories;

internal sealed class RetentionConfigRepository : IRetentionConfigRepository
{
    private readonly EmsPortalDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public RetentionConfigRepository(EmsPortalDbContext dbContext, ITenantContext tenantContext)
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
