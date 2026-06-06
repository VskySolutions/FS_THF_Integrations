using IntegrationHub.Domain.Entities;

namespace IntegrationHub.Application.Abstractions.Persistence;

/// <summary>
/// Data access for <see cref="RetryQueueEntry"/> records. Written and read by the
/// Background Worker's retry framework.
/// </summary>
public interface IRetryQueueRepository
{
    Task AddAsync(RetryQueueEntry entry, CancellationToken cancellationToken = default);

    Task<RetryQueueEntry?> GetByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>Returns pending entries whose next retry time is at or before <paramref name="asOfUtc"/>.</summary>
    Task<IReadOnlyList<RetryQueueEntry>> ListDueAsync(DateTime asOfUtc, CancellationToken cancellationToken = default);

    void Update(RetryQueueEntry entry);

    void Remove(RetryQueueEntry entry);
}
