using global::Hangfire;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Infrastructure.Jobs;
using IntegrationHub.Infrastructure.Retry;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationHub.Workers;

/// <summary>
/// Loads recurring job cron schedules from SQL Server on startup, registers them via
/// <see cref="IRecurringJobManager"/>, and polls every minute to apply runtime schedule
/// changes without a restart (Integration Infrastructure ADR-002). A missing schedule entry
/// for any required job is a startup error (REQ-INF-001).
/// </summary>
public sealed class HangfireJobScheduler : IHostedService, IDisposable
{
    private static readonly string[] RequiredJobs =
    {
        ExpenseImportJob.Name, InvoiceImportJob.Name, VendorPaymentImportJob.Name, RetryJobScheduler.RecurringJobId,
    };

    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRecurringJobManager _recurringJobs;
    private readonly ILogger<HangfireJobScheduler> _logger;
    private readonly Dictionary<string, string> _applied = new(StringComparer.Ordinal);

    private CancellationTokenSource? _pollingCts;
    private Task? _pollingTask;

    public HangfireJobScheduler(IServiceScopeFactory scopeFactory, IRecurringJobManager recurringJobs, ILogger<HangfireJobScheduler> logger)
    {
        _scopeFactory = scopeFactory;
        _recurringJobs = recurringJobs;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var schedules = await LoadSchedulesAsync(cancellationToken);

        var missing = RequiredJobs.Where(j => !schedules.ContainsKey(j)).ToList();
        if (missing.Count > 0)
        {
            // Surface configuration errors early rather than silently skipping jobs.
            throw new InvalidOperationException($"Missing schedule configuration for required jobs: {string.Join(", ", missing)}");
        }

        ApplySchedules(schedules);

        _pollingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pollingTask = PollAsync(_pollingCts.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _pollingCts?.Cancel();
        if (_pollingTask is not null)
        {
            await Task.WhenAny(_pollingTask, Task.Delay(Timeout.Infinite, cancellationToken));
        }
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var schedules = await LoadSchedulesAsync(cancellationToken);
                ApplySchedules(schedules);
            }
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schedule polling failed");
        }
    }

    private async Task<Dictionary<string, string>> LoadSchedulesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IJobScheduleConfigurationRepository>();
        var entries = await repository.ListActiveAsync(cancellationToken);
        return entries.ToDictionary(e => e.JobName, e => e.CronExpression, StringComparer.Ordinal);
    }

    private void ApplySchedules(IReadOnlyDictionary<string, string> schedules)
    {
        foreach (var jobName in RequiredJobs)
        {
            if (!schedules.TryGetValue(jobName, out var cron) || string.IsNullOrWhiteSpace(cron))
            {
                continue;
            }

            // Only re-register when the cron expression actually changed.
            if (_applied.TryGetValue(jobName, out var current) && current == cron)
            {
                continue;
            }

            Register(jobName, cron);
            _applied[jobName] = cron;
            _logger.LogInformation("Registered recurring job {JobName} with cron {Cron}", jobName, cron);
        }
    }

    private void Register(string jobName, string cron)
    {
        if (jobName == ExpenseImportJob.Name)
        {
            _recurringJobs.AddOrUpdate<ExpenseImportJob>(jobName, job => job.RunRecurringAsync(CancellationToken.None), cron);
        }
        else if (jobName == InvoiceImportJob.Name)
        {
            _recurringJobs.AddOrUpdate<InvoiceImportJob>(jobName, job => job.RunRecurringAsync(CancellationToken.None), cron);
        }
        else if (jobName == VendorPaymentImportJob.Name)
        {
            _recurringJobs.AddOrUpdate<VendorPaymentImportJob>(jobName, job => job.RunRecurringAsync(CancellationToken.None), cron);
        }
        else if (jobName == RetryJobScheduler.RecurringJobId)
        {
            _recurringJobs.AddOrUpdate<RetryJobScheduler>(jobName, scheduler => scheduler.ExecuteAsync(CancellationToken.None), cron);
        }
    }

    public void Dispose()
    {
        _pollingCts?.Dispose();
    }
}
