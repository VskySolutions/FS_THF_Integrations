using IntegrationHub.Domain.Entities;

namespace IntegrationHub.Application.Abstractions.Persistence;

/// <summary>
/// Data access for DB-driven recurring job schedules, loaded by the HangfireJobScheduler.
/// </summary>
public interface IJobScheduleConfigurationRepository
{
    Task<IReadOnlyList<JobScheduleConfiguration>> ListActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>All schedules (active and inactive), for admin management.</summary>
    Task<IReadOnlyList<JobScheduleConfiguration>> ListAsync(CancellationToken cancellationToken = default);

    Task<JobScheduleConfiguration?> GetByJobNameAsync(string jobName, CancellationToken cancellationToken = default);

    /// <summary>A schedule for a specific job + tenant (null tenant = the platform-global schedule).</summary>
    Task<JobScheduleConfiguration?> GetAsync(string jobName, Guid? tenantId, CancellationToken cancellationToken = default);

    Task AddAsync(JobScheduleConfiguration configuration, CancellationToken cancellationToken = default);

    void Update(JobScheduleConfiguration configuration);
}
