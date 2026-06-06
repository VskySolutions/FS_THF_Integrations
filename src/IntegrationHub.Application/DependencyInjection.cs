using Microsoft.Extensions.DependencyInjection;

namespace IntegrationHub.Application;

/// <summary>
/// Composition-root entry point for the Application layer. Host projects
/// (Api, Workers, McpServer) call <see cref="AddApplication"/> to register
/// use cases, validators, and orchestration services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers the Application layer services into the DI container.
    /// Concrete registrations are added as use cases are implemented in later work orders.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Application-layer registrations (use cases, validators, mappers) are added here.
        return services;
    }
}
