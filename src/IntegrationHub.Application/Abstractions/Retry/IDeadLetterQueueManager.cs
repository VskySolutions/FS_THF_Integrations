using IntegrationHub.Domain.Entities;

namespace IntegrationHub.Application.Abstractions.Retry;

/// <summary>
/// Performs the atomic dead-letter transition for a job that has exhausted its retries
/// or failed validation: marks the job <c>PermanentlyFailed</c>, writes a final audit
/// entry, and removes the retry queue row — all in one database transaction.
/// </summary>
public interface IDeadLetterQueueManager
{
    Task MoveToDeadLetterAsync(
        IntegrationJob job,
        RetryQueueEntry? retryEntry,
        string reason,
        CancellationToken cancellationToken = default);
}
