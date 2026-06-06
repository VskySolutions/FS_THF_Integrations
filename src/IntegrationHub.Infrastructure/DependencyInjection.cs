using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Security;
using IntegrationHub.Infrastructure.Persistence;
using IntegrationHub.Infrastructure.Persistence.Repositories;
using IntegrationHub.Infrastructure.Security;
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
        services.Configure<ApiKeysOptions>(configuration.GetSection(ConfigurationSections.ApiKeys));

        services.AddSecurity();

        services.AddDbContext<IntegrationHubDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString(ConfigurationSections.SqlServerConnection),
                sql => sql.MigrationsAssembly(typeof(IntegrationHubDbContext).Assembly.FullName)));

        services.AddPersistence();

        // External connectors are registered here in later work orders.
        return services;
    }

    /// <summary>
    /// Registers the unit of work and EF Core repositories. All share the scoped
    /// <see cref="IntegrationHubDbContext"/> so writes commit in a single transaction.
    /// </summary>
    private static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IIntegrationJobRepository, IntegrationJobRepository>();
        services.AddScoped<IIntegrationLogRepository, IntegrationLogRepository>();
        services.AddScoped<IRetryQueueRepository, RetryQueueRepository>();
        services.AddScoped<IMappingConfigurationRepository, MappingConfigurationRepository>();
        services.AddScoped<IAuditTrailRepository, AuditTrailRepository>();
        return services;
    }

    /// <summary>
    /// Registers cross-cutting security services shared by the API and Worker hosts:
    /// the scoped correlation context, the token-version store, and the API key validator.
    /// </summary>
    private static IServiceCollection AddSecurity(this IServiceCollection services)
    {
        services.AddScoped<ICorrelationContext, CorrelationContext>();
        services.AddScoped<IUserSecurityStore, PlaceholderUserSecurityStore>();
        services.AddSingleton<IApiKeyValidator, Pbkdf2ApiKeyValidator>();
        return services;
    }
}
