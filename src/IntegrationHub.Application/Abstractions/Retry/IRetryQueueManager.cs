namespace IntegrationHub.Application.Abstractions.Retry;

/// <summary>
/// Manages the retry lifecycle of failed integration jobs. Single retry-logic path:
/// the scheduler delegates fully to this manager (Error Handling &amp; Retry blueprint).
/// </summary>
public interface IRetryQueueManager
{
    /// <summary>
    /// Records a job failure. Validation failures (<paramref name="isRetriable"/> = false)
    /// are dead-lettered immediately; transient failures are enqueued with the next retry
    /// time per the backoff strategy, or dead-lettered once attempts are exhausted.
    /// </summary>
    Task RegisterFailureAsync(Guid jobId, bool isRetriable, string? error, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-enqueues every retry entry whose next retry time is due. Invoked by the
    /// RetryFailedJobsJob recurring job.
    /// </summary>
    Task ProcessDueRetriesAsync(CancellationToken cancellationToken = default);
}
