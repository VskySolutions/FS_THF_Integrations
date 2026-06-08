using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace IntegrationHub.IntegrationTests;

/// <summary>
/// Boots the real Integration API against a dedicated test database (created by EF Core
/// migrations on first start) so the dev database is never touched. The bootstrap seeder
/// provisions a known Super Admin for authenticated tests.
/// </summary>
public sealed class IntegrationHubApiFactory : WebApplicationFactory<Program>
{
    private const string TestConnectionString =
        "Data Source=VSky-MT\\SQLEXPRESS;Initial Catalog=FS_THF_Integration_Test;User Id=sa;Password=soft;Trusted_Connection=True;MultipleActiveResultSets=True;TrustServerCertificate=True;";

    public const string AdminEmail = "admin@integrationhub.local";
    public const string AdminPassword = "ChangeMe123!";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SqlServer"] = TestConnectionString,
                ["Bootstrap:Email"] = AdminEmail,
                ["Bootstrap:Password"] = AdminPassword,
                ["Bootstrap:TenantIdentifier"] = "system",
                // Disable the SQL Serilog sink noise during tests; console is enough.
                ["Serilog:MinimumLevel"] = "Warning",
            });
        });
    }
}
