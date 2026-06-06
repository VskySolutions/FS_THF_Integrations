using IntegrationHub.Domain.Entities;

namespace IntegrationHub.Application.Abstractions.Persistence;

/// <summary>
/// Data access for DB-driven recurring job schedules, loaded by the HangfireJobScheduler.
/// </summary>
public interface IJobScheduleConfigurationRepository
{
    Task<IReadOnlyList<JobScheduleConfiguration>> ListActiveAsync(CancellationToken cancellationToken = default);

    Task<JobScheduleConfiguration?> GetByJobNameAsync(string jobName, CancellationToken cancellationToken = default);

    Task AddAsync(JobScheduleConfiguration configuration, CancellationToken cancellationToken = default);

    void Update(JobScheduleConfiguration configuration);
}
