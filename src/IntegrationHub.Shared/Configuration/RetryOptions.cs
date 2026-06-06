namespace IntegrationHub.Shared.Configuration;

/// <summary>
/// Retry framework tuning (Error Handling &amp; Retry blueprint). Defaults implement the
/// incremental backoff strategy: 5, 15, 30, 60 minutes across four attempts, then
/// dead-letter. Configurable without redeployment.
/// </summary>
public sealed class RetryOptions
{
    /// <summary>Maximum retry attempts before a job is dead-lettered.</summary>
    public int MaxAttempts { get; set; } = 4;

    /// <summary>Backoff delay (minutes) per attempt number (1-based).</summary>
    public int[] BackoffMinutes { get; set; } = { 5, 15, 30, 60 };

    /// <summary>Cron expression for the RetryFailedJobsJob recurring job (default every 5 min).</summary>
    public string RetryFailedJobsCron { get; set; } = "*/5 * * * *";

    /// <summary>Returns the backoff delay for a given 1-based attempt number.</summary>
    public TimeSpan GetBackoff(int attemptNumber)
    {
        if (BackoffMinutes is null || BackoffMinutes.Length == 0)
        {
            return TimeSpan.FromMinutes(5);
        }

        var index = Math.Clamp(attemptNumber - 1, 0, BackoffMinutes.Length - 1);
        return TimeSpan.FromMinutes(BackoffMinutes[index]);
    }
}
