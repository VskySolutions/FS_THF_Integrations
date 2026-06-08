using IntegrationHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IntegrationHub.Infrastructure.HealthChecks;

/// <summary>
/// Lightweight database probe: runs <c>SELECT 1</c> against the shared SQL Server via
/// the application DbContext (AC-INF-007.4).
/// </summary>
internal sealed class SqlServerHealthCheck : IHealthCheck
{
    public const string Name = "sqlserver";

    private readonly IntegrationHubDbContext _dbContext;

    public SqlServerHealthCheck(IntegrationHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            return HealthCheckResult.Healthy("SQL Server reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQL Server unreachable.", ex);
        }
    }
}
