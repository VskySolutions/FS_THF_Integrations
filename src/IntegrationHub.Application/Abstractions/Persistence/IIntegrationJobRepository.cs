using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Application.Abstractions.Persistence;

/// <summary>
/// Data access for <see cref="IntegrationJob"/> records. Writes are staged on the
/// shared unit of work and committed via <see cref="IUnitOfWork.SaveChangesAsync"/>.
/// </summary>
public interface IIntegrationJobRepository
{
    Task<IntegrationJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IntegrationJob>> ListByStatusAsync(IntegrationJobStatus status, CancellationToken cancellationToken = default);

    Task AddAsync(IntegrationJob job, CancellationToken cancellationToken = default);

    void Update(IntegrationJob job);

    /// <summary>True if the tenant has any Created/Running jobs (blocks tenant archive).</summary>
    Task<bool> HasActiveJobsAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
