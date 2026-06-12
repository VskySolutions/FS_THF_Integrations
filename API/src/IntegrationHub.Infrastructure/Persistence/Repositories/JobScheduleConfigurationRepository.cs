using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence.Repositories;

internal sealed class JobScheduleConfigurationRepository : IJobScheduleConfigurationRepository
{
    private readonly IntegrationHubDbContext _dbContext;

    public JobScheduleConfigurationRepository(IntegrationHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<JobScheduleConfiguration>> ListActiveAsync(CancellationToken cancellationToken = default)
        => await _dbContext.JobScheduleConfigurations
            .Where(j => j.IsActive)
            .OrderBy(j => j.JobName)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<JobScheduleConfiguration>> ListAsync(CancellationToken cancellationToken = default)
        => await _dbContext.JobScheduleConfigurations
            .OrderBy(j => j.JobName)
            .ToListAsync(cancellationToken);

    public Task<JobScheduleConfiguration?> GetByJobNameAsync(string jobName, CancellationToken cancellationToken = default)
        => _dbContext.JobScheduleConfigurations.FirstOrDefaultAsync(j => j.JobName == jobName, cancellationToken);

    public Task<JobScheduleConfiguration?> GetAsync(string jobName, Guid? tenantId, CancellationToken cancellationToken = default)
        => _dbContext.JobScheduleConfigurations.FirstOrDefaultAsync(j => j.JobName == jobName && j.TenantId == tenantId, cancellationToken);

    public async Task AddAsync(JobScheduleConfiguration configuration, CancellationToken cancellationToken = default)
        => await _dbContext.JobScheduleConfigurations.AddAsync(configuration, cancellationToken);

    public void Update(JobScheduleConfiguration configuration)
        => _dbContext.JobScheduleConfigurations.Update(configuration);
}
