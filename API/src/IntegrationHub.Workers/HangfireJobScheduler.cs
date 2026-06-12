using global::Hangfire;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Infrastructure.Jobs;
using IntegrationHub.Infrastructure.Retry;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationHub.Workers;

/// <summary>
/// Loads recurring job schedules from SQL Server on startup, registers them via
/// <see cref="IRecurringJobManager"/>, and polls every minute to apply runtime changes without a
/// restart (Integration Infrastructure ADR-002).
///
/// Schedules can be **global** (TenantId null) or **per-tenant** (TenantId set):
/// - A global import schedule registers the fan-out job <c>{JobName}</c> (RunRecurringAsync) which
///   imports for every active tenant — except tenants that have their own active per-tenant schedule.
/// - A per-tenant import schedule registers <c>{JobName}:{TenantId}</c> (RunForTenantAsync).
/// - The retry job is always global.
/// Per-tenant recurring jobs are removed when their schedule is deactivated/deleted.
/// The four global schedules remain required at startup (REQ-INF-001).
/// </summary>
public sealed class HangfireJobScheduler : IHostedService, IDisposable
{
    private static readonly string[] ImportJobs =
    {
        ExpenseImportJob.Name, InvoiceImportJob.Name, VendorPaymentImportJob.Name,
    };

    private static readonly string[] RequiredGlobalJobs =
    {
        ExpenseImportJob.Name, InvoiceImportJob.Name, VendorPaymentImportJob.Name, RetryJobScheduler.RecurringJobId,
    };

    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRecurringJobManager _recurringJobs;
    private readonly ILogger<HangfireJobScheduler> _logger;
    private readonly Dictionary<string, string> _applied = new(StringComparer.Ordinal); // recurringJobId -> cron

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

        var globalNames = schedules.Where(s => s.TenantId is null).Select(s => s.JobName).ToHashSet(StringComparer.Ordinal);
        var missing = RequiredGlobalJobs.Where(j => !globalNames.Contains(j)).ToList();
        if (missing.Count > 0)
        {
            // Surface configuration errors early rather than silently skipping jobs.
            throw new InvalidOperationException($"Missing global schedule configuration for required jobs: {string.Join(", ", missing)}");
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

    private async Task<IReadOnlyList<JobScheduleConfiguration>> LoadSchedulesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IJobScheduleConfigurationRepository>();
        return await repository.ListActiveAsync(cancellationToken);
    }

    private void ApplySchedules(IReadOnlyList<JobScheduleConfiguration> schedules)
    {
        // Build the desired set of recurring jobs from the active schedules.
        var desired = new Dictionary<string, (string Cron, string JobName, Guid? TenantId)>(StringComparer.Ordinal);
        foreach (var s in schedules)
        {
            if (string.IsNullOrWhiteSpace(s.CronExpression))
            {
                continue;
            }

            if (s.TenantId is { } tenantId && ImportJobs.Contains(s.JobName))
            {
                desired[$"{s.JobName}:{tenantId}"] = (s.CronExpression, s.JobName, tenantId);
            }
            else if (s.TenantId is null && (ImportJobs.Contains(s.JobName) || s.JobName == RetryJobScheduler.RecurringJobId))
            {
                desired[s.JobName] = (s.CronExpression, s.JobName, null);
            }
        }

        // Add / update (only when the cron actually changed).
        foreach (var (id, entry) in desired)
        {
            if (_applied.TryGetValue(id, out var current) && current == entry.Cron)
            {
                continue;
            }

            Register(id, entry.JobName, entry.TenantId, entry.Cron);
            _applied[id] = entry.Cron;
            _logger.LogInformation("Registered recurring job {Id} with cron {Cron}", id, entry.Cron);
        }

        // Remove recurring jobs whose schedule was deactivated or deleted.
        foreach (var id in _applied.Keys.Where(k => !desired.ContainsKey(k)).ToList())
        {
            _recurringJobs.RemoveIfExists(id);
            _applied.Remove(id);
            _logger.LogInformation("Removed recurring job {Id}", id);
        }
    }

    private void Register(string recurringJobId, string jobName, Guid? tenantId, string cron)
    {
        if (tenantId is { } tid)
        {
            // Per-tenant import: import only for this tenant.
            if (jobName == ExpenseImportJob.Name)
            {
                _recurringJobs.AddOrUpdate<ExpenseImportJob>(recurringJobId, job => job.RunForTenantAsync(tid, CancellationToken.None), cron);
            }
            else if (jobName == InvoiceImportJob.Name)
            {
                _recurringJobs.AddOrUpdate<InvoiceImportJob>(recurringJobId, job => job.RunForTenantAsync(tid, CancellationToken.None), cron);
            }
            else if (jobName == VendorPaymentImportJob.Name)
            {
                _recurringJobs.AddOrUpdate<VendorPaymentImportJob>(recurringJobId, job => job.RunForTenantAsync(tid, CancellationToken.None), cron);
            }

            return;
        }

        // Global: import fan-out across all active tenants, or the retry sweep.
        if (jobName == ExpenseImportJob.Name)
        {
            _recurringJobs.AddOrUpdate<ExpenseImportJob>(recurringJobId, job => job.RunRecurringAsync(CancellationToken.None), cron);
        }
        else if (jobName == InvoiceImportJob.Name)
        {
            _recurringJobs.AddOrUpdate<InvoiceImportJob>(recurringJobId, job => job.RunRecurringAsync(CancellationToken.None), cron);
        }
        else if (jobName == VendorPaymentImportJob.Name)
        {
            _recurringJobs.AddOrUpdate<VendorPaymentImportJob>(recurringJobId, job => job.RunRecurringAsync(CancellationToken.None), cron);
        }
        else if (jobName == RetryJobScheduler.RecurringJobId)
        {
            _recurringJobs.AddOrUpdate<RetryJobScheduler>(recurringJobId, scheduler => scheduler.ExecuteAsync(CancellationToken.None), cron);
        }
    }

    public void Dispose()
    {
        _pollingCts?.Dispose();
    }
}
