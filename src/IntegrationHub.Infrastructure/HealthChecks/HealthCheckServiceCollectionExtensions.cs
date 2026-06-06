using Microsoft.Extensions.DependencyInjection;

namespace IntegrationHub.Infrastructure.HealthChecks;

/// <summary>
/// Registers the platform health checks: SQL Server plus the three external system
/// connectivity probes. All are tagged <see cref="ReadyTag"/> so the readiness endpoint
/// reflects dependency availability (REQ-INF-007).
/// </summary>
public static class IntegrationHubHealthCheckExtensions
{
    /// <summary>Tag marking checks that gate readiness (all dependency probes).</summary>
    public const string ReadyTag = "ready";

    public static IServiceCollection AddIntegrationHubHealthChecks(this IServiceCollection services)
    {
        services.AddHttpClient();

        services.AddHealthChecks()
            .AddCheck<SqlServerHealthCheck>(SqlServerHealthCheck.Name, tags: new[] { ReadyTag })
            .AddCheck<PaycorApiHealthCheck>(PaycorApiHealthCheck.Name, tags: new[] { ReadyTag })
            .AddCheck<ConcurApiHealthCheck>(ConcurApiHealthCheck.Name, tags: new[] { ReadyTag })
            .AddCheck<MaconomyApiHealthCheck>(MaconomyApiHealthCheck.Name, tags: new[] { ReadyTag });

        return services;
    }
}
