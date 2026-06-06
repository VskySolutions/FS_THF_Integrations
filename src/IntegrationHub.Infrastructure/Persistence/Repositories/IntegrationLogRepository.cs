using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence.Repositories;

internal sealed class IntegrationLogRepository : IIntegrationLogRepository
{
    private readonly IntegrationHubDbContext _dbContext;

    public IntegrationLogRepository(IntegrationHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(IntegrationLog log, CancellationToken cancellationToken = default)
        => await _dbContext.IntegrationLogs.AddAsync(log, cancellationToken);

    public async Task<IReadOnlyList<IntegrationLog>> ListByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default)
        => await _dbContext.IntegrationLogs
            .Where(l => l.JobId == jobId)
            .OrderBy(l => l.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<IntegrationLog> Items, int Total)> QueryAsync(
        Guid? tenantId, Guid? jobId, string? level, DateTime? fromDate, DateTime? toDate,
        int page, int limit, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.IntegrationLogs.IgnoreQueryFilters().AsQueryable();
        if (tenantId is { } tid) { query = query.Where(l => l.TenantId == tid); }
        if (jobId is { } jid) { query = query.Where(l => l.JobId == jid); }
        if (!string.IsNullOrWhiteSpace(level)) { query = query.Where(l => l.Level == level); }
        if (fromDate is { } from) { query = query.Where(l => l.CreatedAtUtc >= from); }
        if (toDate is { } to) { query = query.Where(l => l.CreatedAtUtc <= to); }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(l => l.CreatedAtUtc)
            .Skip((page - 1) * limit).Take(limit).ToListAsync(cancellationToken);
        return (items, total);
    }
}
