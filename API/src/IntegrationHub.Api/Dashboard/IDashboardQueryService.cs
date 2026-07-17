using IntegrationHub.Api.Models.Dashboard;

namespace IntegrationHub.Api.Dashboard;

/// <summary>
/// Aggregates the dashboard read models from across the platform's data sources (customers,
/// users, tenants). Scoped: shares the request's DbContext and tenant scope.
/// </summary>
public interface IDashboardQueryService
{
    Task<CustomerDashboardDto> GetCustomersAsync(Guid? tenantId, string dateRange, CancellationToken cancellationToken);

    Task<UserDashboardDto> GetUsersAsync(Guid? tenantId, string dateRange, CancellationToken cancellationToken);

    Task<PlatformDashboardDto> GetPlatformAsync(string dateRange, bool forceRefresh, CancellationToken cancellationToken);
}
