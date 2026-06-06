using IntegrationHub.Infrastructure.Persistence;
using IntegrationHub.Shared.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationHub.Infrastructure;

/// <summary>
/// Composition-root entry point for the Infrastructure layer. Host projects
/// (Api, Workers, McpServer) call <see cref="AddInfrastructure"/> to register
/// data access, external connectors, and infrastructure-bound options.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers the Infrastructure layer services and binds shared configuration
    /// sections into strongly-typed options. Persistence, connectors, and queue wiring
    /// are added in later work orders.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<HangfireOptions>(configuration.GetSection(ConfigurationSections.Hangfire));
        services.Configure<AuthenticationOptions>(configuration.GetSection(ConfigurationSections.Authentication));
        services.Configure<PaycorOptions>(configuration.GetSection(ConfigurationSections.Paycor));
        services.Configure<ConcurOptions>(configuration.GetSection(ConfigurationSections.Concur));
        services.Configure<MaconomyOptions>(configuration.GetSection(ConfigurationSections.Maconomy));

        services.AddDbContext<IntegrationHubDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString(ConfigurationSections.SqlServerConnection),
                sql => sql.MigrationsAssembly(typeof(IntegrationHubDbContext).Assembly.FullName)));

        // Repositories and external connectors are registered here in later work orders.
        return services;
    }
}
