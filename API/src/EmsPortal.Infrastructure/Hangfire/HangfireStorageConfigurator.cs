using global::Hangfire;
using global::Hangfire.SqlServer;
using EmsPortal.Shared.Configuration;
using Microsoft.Extensions.Configuration;

namespace EmsPortal.Infrastructure.Hangfire;

/// <summary>
/// Shared Hangfire SQL Server storage configuration used by both the Background Worker
/// (server) and the Integration API (dashboard). The Hangfire schema is auto-provisioned
/// on first connection (Background Worker blueprint).
/// </summary>
public static class HangfireStorageConfigurator
{
    /// <summary>True when a SQL Server connection string is configured for Hangfire.</summary>
    public static bool IsConfigured(IConfiguration configuration)
        => !string.IsNullOrWhiteSpace(configuration.GetConnectionString(ConfigurationSections.SqlServerConnection));

    /// <summary>Applies SQL Server storage with the configured schema name.</summary>
    public static IGlobalConfiguration Configure(IGlobalConfiguration configuration, IConfiguration appConfiguration)
    {
        var connectionString = appConfiguration.GetConnectionString(ConfigurationSections.SqlServerConnection);
        var hangfireOptions = appConfiguration.GetSection(ConfigurationSections.Hangfire).Get<HangfireOptions>()
            ?? new HangfireOptions();

        return configuration
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
            {
                SchemaName = string.IsNullOrWhiteSpace(hangfireOptions.SchemaName) ? "HangFire" : hangfireOptions.SchemaName,
                PrepareSchemaIfNecessary = true,
                QueuePollInterval = TimeSpan.FromSeconds(15),
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true,
            });
    }
}
