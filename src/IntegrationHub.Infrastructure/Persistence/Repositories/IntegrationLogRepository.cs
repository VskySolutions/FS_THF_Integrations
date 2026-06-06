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
}
