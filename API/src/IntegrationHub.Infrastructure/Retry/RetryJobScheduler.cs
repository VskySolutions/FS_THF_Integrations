using global::Hangfire;
using IntegrationHub.Application.Abstractions.Retry;

namespace IntegrationHub.Infrastructure.Retry;

/// <summary>
/// The <c>RetryFailedJobsJob</c> recurring Hangfire job. Holds no retry logic itself —
/// it delegates entirely to <see cref="IRetryQueueManager"/> so there is a single retry
/// path. <see cref="DisableConcurrentExecutionAttribute"/> prevents overlapping runs.
/// </summary>
public sealed class RetryJobScheduler
{
    public const string RecurringJobId = "RetryFailedJobsJob";

    private readonly IRetryQueueManager _retryQueueManager;

    public RetryJobScheduler(IRetryQueueManager retryQueueManager)
    {
        _retryQueueManager = retryQueueManager;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public Task ExecuteAsync(CancellationToken cancellationToken)
        => _retryQueueManager.ProcessDueRetriesAsync(cancellationToken);
}
