using IntegrationHub.Application.Abstractions.Auditing;
using IntegrationHub.Application.Abstractions.Connectors;
using IntegrationHub.Application.Abstractions.Connectors.Concur;
using IntegrationHub.Application.Abstractions.Connectors.Maconomy;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Retry;
using IntegrationHub.Application.Abstractions.Security;
using IntegrationHub.Application.Abstractions.Tenancy;
using IntegrationHub.Infrastructure.Auditing;
using IntegrationHub.Infrastructure.Connectors.Concur;
using IntegrationHub.Infrastructure.Connectors.Maconomy;
using IntegrationHub.Infrastructure.Tenancy;
using IntegrationHub.Infrastructure.Persistence;
using IntegrationHub.Infrastructure.Persistence.Repositories;
using IntegrationHub.Infrastructure.Retry;
using IntegrationHub.Infrastructure.Security;
using IntegrationHub.Shared.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        services.Configure<RetryOptions>(configuration.GetSection(ConfigurationSections.Retry));

        services.AddSecurity();
        services.AddRetry();
        services.AddConnectors();

        services.AddDbContext<IntegrationHubDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString(ConfigurationSections.SqlServerConnection),
                sql => sql.MigrationsAssembly(typeof(IntegrationHubDbContext).Assembly.FullName)));

        services.AddPersistence();

        // Persist the Data Protection key ring to SQL Server so every API/Worker instance
        // shares the same keys (Multi-Tenancy ADR-002). Application name isolates the ring.
        services.AddDataProtection()
            .PersistKeysToDbContext<IntegrationHubDbContext>()
            .SetApplicationName("IntegrationHub");

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
        services.AddScoped<IAuditTrailService, AuditTrailService>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantApiConfigurationRepository, TenantApiConfigurationRepository>();
        services.AddScoped<IJobScheduleConfigurationRepository, JobScheduleConfigurationRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPersonRepository, PersonRepository>();
        services.AddScoped<IAddressRepository, AddressRepository>();
        services.AddScoped<IMediaRepository, MediaRepository>();
        services.AddScoped<ICustomerRequestRepository, CustomerRequestRepository>();
        services.AddScoped<ICustomerAuditRepository, CustomerAuditRepository>();
        services.AddScoped<ICustomerDocumentRepository, CustomerDocumentRepository>();
        services.AddScoped<IPermissionGroupRepository, PermissionGroupRepository>();
        services.AddScoped<IDashboardLayoutRepository, DashboardLayoutRepository>();
        services.AddSingleton<ITransformationRuleEvaluator, Connectors.TransformationRuleEvaluator>();
        return services;
    }

    /// <summary>
    /// Registers cross-cutting security services shared by the API and Worker hosts:
    /// the scoped correlation context, the token-version store, and the API key validator.
    /// </summary>
    private static IServiceCollection AddSecurity(this IServiceCollection services)
    {
        services.AddScoped<ICorrelationContext, CorrelationContext>();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ITokenVersionValidator, DbTokenVersionValidator>();
        services.AddSingleton<IApiKeyValidator, Pbkdf2ApiKeyValidator>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ISigningKeyProvider, RsaSigningKeyProvider>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        // Default actor identity is the system; the API replaces this with an
        // HttpContext-based accessor that resolves the authenticated user.
        services.TryAddScoped<IActorAccessor, SystemActorAccessor>();
        return services;
    }

    /// <summary>
    /// Registers the retry framework: the retry/dead-letter managers, the recurring-job
    /// target, and the (placeholder) job executor.
    /// </summary>
    private static IServiceCollection AddRetry(this IServiceCollection services)
    {
        services.AddScoped<IDeadLetterQueueManager, DeadLetterQueueManager>();
        services.AddScoped<IRetryQueueManager, RetryQueueManager>();
        services.AddScoped<IIntegrationJobExecutor, PlaceholderIntegrationJobExecutor>();
        services.AddScoped<RetryJobScheduler>();
        return services;
    }

    /// <summary>
    /// Registers the connector framework runtime: per-tenant credential resolution and the
    /// external system connectors (Concur, Maconomy). Connectors hold in-memory token state,
    /// so they are scoped to a request/job.
    /// </summary>
    private static IServiceCollection AddConnectors(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddSingleton<ICredentialEncryptionService, DataProtectionCredentialEncryptionService>();
        services.AddScoped<ITenantApiConfigurationService, TenantApiConfigurationService>();
        services.AddScoped<IConcurConnector, ConcurConnector>();
        services.AddScoped<IMaconomyConnector, MaconomyConnector>();

        // Recurring/triggered Concur import job classes (Hangfire resolves them from DI).
        services.AddScoped<Jobs.ExpenseImportJob>();
        services.AddScoped<Jobs.InvoiceImportJob>();
        services.AddScoped<Jobs.VendorPaymentImportJob>();

        // On-demand Customer → Maconomy sync job + its dispatcher.
        services.AddScoped<Jobs.CustomerSyncJob>();
        services.AddScoped<Application.Abstractions.Customers.ICustomerSyncDispatcher, Jobs.CustomerSyncDispatcher>();
        return services;
    }
}
