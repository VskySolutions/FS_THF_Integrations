using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence.Repositories;

internal sealed class RetryQueueRepository : IRetryQueueRepository
{
    private readonly IntegrationHubDbContext _dbContext;

    public RetryQueueRepository(IntegrationHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(RetryQueueEntry entry, CancellationToken cancellationToken = default)
        => await _dbContext.RetryQueue.AddAsync(entry, cancellationToken);

    public Task<RetryQueueEntry?> GetByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default)
        => _dbContext.RetryQueue.FirstOrDefaultAsync(r => r.JobId == jobId, cancellationToken);

    public async Task<IReadOnlyList<RetryQueueEntry>> ListDueAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
        => await _dbContext.RetryQueue
            .Where(r => r.Status == RetryStatus.Pending && r.NextRetryDate <= asOfUtc)
            .OrderBy(r => r.NextRetryDate)
            .ToListAsync(cancellationToken);

    public void Update(RetryQueueEntry entry)
        => _dbContext.RetryQueue.Update(entry);

    public void Remove(RetryQueueEntry entry)
        => _dbContext.RetryQueue.Remove(entry);

    public async Task<(IReadOnlyList<RetryQueueEntry> Items, int Total)> QueryAsync(
        Guid? tenantId, int page, int limit, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.RetryQueue.IgnoreQueryFilters().AsQueryable();
        if (tenantId is { } tid) { query = query.Where(r => r.TenantId == tid); }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(r => r.UpdatedOnUtc)
            .Skip((page - 1) * limit).Take(limit).ToListAsync(cancellationToken);
        return (items, total);
    }
}
