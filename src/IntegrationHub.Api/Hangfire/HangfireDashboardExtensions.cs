using global::Hangfire;
using global::Hangfire.Dashboard;
using IntegrationHub.Infrastructure.Hangfire;
using IntegrationHub.Shared.Configuration;
using IntegrationHub.Shared.Security;

namespace IntegrationHub.Api.Hangfire;

/// <summary>
/// Registers Hangfire storage so the Integration API can host the monitoring
/// dashboard at <c>/hangfire</c>, restricted to admin roles via the authorization
/// pipeline. The API enqueues jobs; the Background Worker executes them.
/// </summary>
public static class HangfireDashboardExtensions
{
    public static IServiceCollection AddIntegrationHubHangfireDashboard(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (HangfireStorageConfigurator.IsConfigured(configuration))
        {
            services.AddHangfire(config => HangfireStorageConfigurator.Configure(config, configuration));
        }

        return services;
    }

    public static IApplicationBuilder UseIntegrationHubHangfireDashboard(
        this WebApplication app,
        IConfiguration configuration)
    {
        var options = configuration.GetSection(ConfigurationSections.Hangfire).Get<HangfireOptions>()
            ?? new HangfireOptions();

        if (!options.DashboardEnabled || !HangfireStorageConfigurator.IsConfigured(configuration))
        {
            return app;
        }

        // ASP.NET authorization (TenantAdminOrAbove) gates the dashboard; Hangfire's
        // own local-only filter is cleared so the policy is the single gate.
        app.MapHangfireDashboard("/hangfire", new DashboardOptions
            {
                Authorization = Array.Empty<IDashboardAuthorizationFilter>(),
            })
            .RequireAuthorization(AuthorizationPolicies.TenantAdminOrAbove);

        return app;
    }
}
