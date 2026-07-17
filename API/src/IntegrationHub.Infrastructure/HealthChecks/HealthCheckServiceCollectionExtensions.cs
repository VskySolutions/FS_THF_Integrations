using Microsoft.Extensions.DependencyInjection;

namespace IntegrationHub.Infrastructure.HealthChecks;

/// <summary>
/// Registers the platform health checks: the SQL Server connectivity probe, tagged
/// <see cref="ReadyTag"/> so the readiness endpoint reflects dependency availability.
/// </summary>
public static class IntegrationHubHealthCheckExtensions
{
    /// <summary>Tag marking checks that gate readiness (all dependency probes).</summary>
    public const string ReadyTag = "ready";

    public static IServiceCollection AddIntegrationHubHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<SqlServerHealthCheck>(SqlServerHealthCheck.Name, tags: new[] { ReadyTag });

        return services;
    }
}
