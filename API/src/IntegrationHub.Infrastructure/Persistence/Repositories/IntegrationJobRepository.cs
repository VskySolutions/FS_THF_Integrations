using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence.Repositories;

internal sealed class IntegrationJobRepository : IIntegrationJobRepository
{
    private readonly IntegrationHubDbContext _dbContext;

    public IntegrationJobRepository(IntegrationHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<IntegrationJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.IntegrationJobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    public Task<IntegrationJob?> GetByIdUnscopedAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.IntegrationJobs.IgnoreQueryFilters().FirstOrDefaultAsync(j => j.Id == id && !j.Deleted, cancellationToken);

    public async Task<IReadOnlyList<IntegrationJob>> ListByStatusAsync(IntegrationJobStatus status, CancellationToken cancellationToken = default)
        => await _dbContext.IntegrationJobs
            .Where(j => j.Status == status)
            .OrderByDescending(j => j.UpdatedOnUtc)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(IntegrationJob job, CancellationToken cancellationToken = default)
        => await _dbContext.IntegrationJobs.AddAsync(job, cancellationToken);

    public void Update(IntegrationJob job)
        => _dbContext.IntegrationJobs.Update(job);

    public void Remove(IntegrationJob job)
        => _dbContext.IntegrationJobs.Remove(job);

    public Task<bool> HasActiveJobsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _dbContext.IntegrationJobs.IgnoreQueryFilters()
            .AnyAsync(j => j.TenantId == tenantId
                && (j.Status == IntegrationJobStatus.Created || j.Status == IntegrationJobStatus.Running), cancellationToken);

    public async Task<(IReadOnlyList<IntegrationJob> Items, int Total)> QueryAsync(
        Guid? tenantId, IntegrationJobStatus? status, string? interfaceName, DateTime? fromDate, DateTime? toDate,
        int page, int limit, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.IntegrationJobs.IgnoreQueryFilters().AsQueryable();
        if (tenantId is { } tid) { query = query.Where(j => j.TenantId == tid); }
        if (status is { } st) { query = query.Where(j => j.Status == st); }
        if (!string.IsNullOrWhiteSpace(interfaceName)) { query = query.Where(j => j.InterfaceName == interfaceName); }
        if (fromDate is { } from) { query = query.Where(j => j.CreatedAtUtc >= from); }
        if (toDate is { } to) { query = query.Where(j => j.CreatedAtUtc <= to); }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(j => j.UpdatedOnUtc)
            .Skip((page - 1) * limit).Take(limit).ToListAsync(cancellationToken);
        return (items, total);
    }
}
