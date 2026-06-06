using global::Hangfire;
using IntegrationHub.Infrastructure.Retry;
using IntegrationHub.Shared.Configuration;
using Microsoft.Extensions.Options;

namespace IntegrationHub.Workers;

/// <summary>
/// Registers the platform's recurring Hangfire jobs on startup. For now this is the
/// RetryFailedJobsJob (WO-9); the DB-driven schedule loader (WO-30) will expand this.
/// </summary>
public sealed class RecurringJobRegistrar : IHostedService
{
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly RetryOptions _retryOptions;

    public RecurringJobRegistrar(IRecurringJobManager recurringJobManager, IOptions<RetryOptions> retryOptions)
    {
        _recurringJobManager = recurringJobManager;
        _retryOptions = retryOptions.Value;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var cron = string.IsNullOrWhiteSpace(_retryOptions.RetryFailedJobsCron)
            ? "*/5 * * * *"
            : _retryOptions.RetryFailedJobsCron;

        _recurringJobManager.AddOrUpdate<RetryJobScheduler>(
            RetryJobScheduler.RecurringJobId,
            scheduler => scheduler.ExecuteAsync(CancellationToken.None),
            cron);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
