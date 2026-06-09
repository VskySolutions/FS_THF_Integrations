namespace IntegrationHub.Shared.Security;

/// <summary>
/// The platform permission catalogue. Custom roles (RBAC) are composed of these
/// permission keys; API endpoints are gated by them (replacing fixed role policies
/// over the RBAC rollout). Keys are stable strings of the form "area.action".
/// </summary>
public static class Permissions
{
    // Tenants
    public const string TenantsRead = "tenants.read";
    public const string TenantsWrite = "tenants.write";
    public const string TenantsArchive = "tenants.archive";
    public const string TenantsCredentials = "tenants.credentials";

    // Users
    public const string UsersRead = "users.read";
    public const string UsersWrite = "users.write";
    public const string UsersResetPassword = "users.reset_password";

    // Roles (RBAC management)
    public const string RolesRead = "roles.read";
    public const string RolesWrite = "roles.write";
    public const string RolesAssign = "roles.assign";

    // Mappings
    public const string MappingsRead = "mappings.read";
    public const string MappingsWrite = "mappings.write";

    // Integration jobs / logs
    public const string JobsRead = "jobs.read";
    public const string JobsTrigger = "jobs.trigger";
    public const string JobsRetry = "jobs.retry";
    public const string LogsRead = "logs.read";

    // Platform
    public const string HealthRead = "health.read";

    /// <summary>Every defined permission key.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        TenantsRead, TenantsWrite, TenantsArchive, TenantsCredentials,
        UsersRead, UsersWrite, UsersResetPassword,
        RolesRead, RolesWrite, RolesAssign,
        MappingsRead, MappingsWrite,
        JobsRead, JobsTrigger, JobsRetry, LogsRead,
        HealthRead
    };

    /// <summary>Permission sets for the seeded system roles.</summary>
    public static IReadOnlyList<string> ForSuperAdmin() => All;

    public static IReadOnlyList<string> ForTenantAdmin() => new[]
    {
        TenantsRead, TenantsCredentials,
        UsersRead, UsersWrite, UsersResetPassword,
        RolesRead, RolesAssign,
        MappingsRead, MappingsWrite,
        JobsRead, JobsTrigger, JobsRetry, LogsRead,
        HealthRead
    };

    public static IReadOnlyList<string> ForOperator() => new[]
    {
        JobsRead, JobsTrigger, LogsRead
    };
}
