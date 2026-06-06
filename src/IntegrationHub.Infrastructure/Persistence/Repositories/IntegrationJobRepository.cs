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

    public async Task<IReadOnlyList<IntegrationJob>> ListByStatusAsync(IntegrationJobStatus status, CancellationToken cancellationToken = default)
        => await _dbContext.IntegrationJobs
            .Where(j => j.Status == status)
            .OrderByDescending(j => j.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(IntegrationJob job, CancellationToken cancellationToken = default)
        => await _dbContext.IntegrationJobs.AddAsync(job, cancellationToken);

    public void Update(IntegrationJob job)
        => _dbContext.IntegrationJobs.Update(job);

    public Task<bool> HasActiveJobsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _dbContext.IntegrationJobs.IgnoreQueryFilters()
            .AnyAsync(j => j.TenantId == tenantId
                && (j.Status == IntegrationJobStatus.Created || j.Status == IntegrationJobStatus.Running), cancellationToken);
}
