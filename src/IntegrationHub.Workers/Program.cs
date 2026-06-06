using global::Hangfire;
using IntegrationHub.Application;
using IntegrationHub.Infrastructure;
using IntegrationHub.Infrastructure.Hangfire;
using IntegrationHub.Infrastructure.Logging;
using IntegrationHub.Shared.Configuration;
using IntegrationHub.Workers;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Structured logging to SQL Server, enriched with correlation ID, service, environment.
builder.Services.AddSerilog((_, loggerConfiguration) =>
    SerilogConfigurator.Configure(
        loggerConfiguration,
        builder.Configuration,
        "IntegrationHub.Workers",
        builder.Environment.EnvironmentName));

// Clean Architecture composition root.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Hangfire server: consumes jobs from the shared SQL Server queue (schema auto-provisioned).
// AddHangfireServer registers an IHostedService that drains in-flight jobs on the host's
// cancellation token for graceful shutdown. Recurring job registration is a later work order.
if (HangfireStorageConfigurator.IsConfigured(builder.Configuration))
{
    var hangfireOptions = builder.Configuration.GetSection(ConfigurationSections.Hangfire).Get<HangfireOptions>()
        ?? new HangfireOptions();

    // Reconstruct ITenantContext from each job payload before the handler runs.
    GlobalJobFilters.Filters.Add(new TenantHangfireJobFilter());

    builder.Services.AddHangfire(config => HangfireStorageConfigurator.Configure(config, builder.Configuration));
    builder.Services.AddHangfireServer(options =>
    {
        options.WorkerCount = hangfireOptions.WorkerCount > 0 ? hangfireOptions.WorkerCount : Environment.ProcessorCount * 5;
        if (!string.IsNullOrWhiteSpace(hangfireOptions.ServerName))
        {
            options.ServerName = hangfireOptions.ServerName;
        }
    });

    // Registers the RetryFailedJobsJob recurring job once the server is up.
    builder.Services.AddHostedService<RecurringJobRegistrar>();
}

var host = builder.Build();
host.Run();
