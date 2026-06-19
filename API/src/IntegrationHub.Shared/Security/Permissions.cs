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

    // Persons (CRM master records — the precursor to a login account)
    public const string PersonsRead = "persons.read";
    public const string PersonsWrite = "persons.write";
    public const string PersonsDelete = "persons.delete";

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
    public const string JobsDelete = "jobs.delete";
    /// <summary>Manage recurring job schedules (platform-global; Super-Admin only by default).</summary>
    public const string JobsSchedule = "jobs.schedule";
    public const string LogsRead = "logs.read";

    // Customer Management workflow
    /// <summary>Create / enter Customer Requests (Step 1 Basic Information) and save drafts.</summary>
    public const string CustomersDataEntry = "customers.dataEntry";
    /// <summary>Enrich submitted Customer Requests and send them for approval (Customer Role User).</summary>
    public const string CustomersReview = "customers.review";
    /// <summary>Complete Step 2 Maconomy fields and approve/reject/return Customer Requests (Customer Approver).</summary>
    public const string CustomersApprove = "customers.approve";

    // Permission Groups (RBAC composition layer)
    /// <summary>Create, edit, and compose Permission Groups (and compose them into roles).</summary>
    public const string GroupsManage = "groups.manage";

    // Platform
    public const string HealthRead = "health.read";

    /// <summary>Every defined permission key.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        TenantsRead, TenantsWrite, TenantsArchive, TenantsCredentials,
        PersonsRead, PersonsWrite, PersonsDelete,
        UsersRead, UsersWrite, UsersResetPassword,
        RolesRead, RolesWrite, RolesAssign,
        MappingsRead, MappingsWrite,
        JobsRead, JobsTrigger, JobsRetry, JobsDelete, JobsSchedule, LogsRead,
        CustomersDataEntry, CustomersReview, CustomersApprove,
        GroupsManage,
        HealthRead
    };

    /// <summary>Permission sets for the seeded system roles.</summary>
    public static IReadOnlyList<string> ForSuperAdmin() => All;

    public static IReadOnlyList<string> ForTenantAdmin() => new[]
    {
        TenantsRead, TenantsCredentials,
        // Deleting persons and changing role assignments are Super-Admin-only (PersonsDelete /
        // RolesAssign intentionally excluded here).
        PersonsRead, PersonsWrite,
        UsersRead, UsersWrite, UsersResetPassword,
        RolesRead,
        MappingsRead, MappingsWrite,
        JobsRead, JobsTrigger, JobsRetry, JobsDelete, JobsSchedule, LogsRead,
        // Tenant Admins act as Customer data-entry, Customer Role User and Customer Approver.
        CustomersDataEntry, CustomersReview, CustomersApprove,
        // Tenant Admins manage Permission Groups within their own tenant.
        GroupsManage,
        HealthRead
    };

    public static IReadOnlyList<string> ForOperator() => new[]
    {
        JobsRead, JobsTrigger, LogsRead
    };

    /// <summary>
    /// The seeded permission set for a system role name (SuperAdmin/TenantAdmin/Operator), or an
    /// empty set for an unrecognised name. Used as the fallback when a caller carries only a role
    /// claim (API-key callers, pre-RBAC tokens) and no explicit permission claims.
    /// </summary>
    public static IReadOnlyList<string> ForSystemRole(string? roleName) => roleName switch
    {
        Roles.SuperAdmin => ForSuperAdmin(),
        Roles.TenantAdmin => ForTenantAdmin(),
        Roles.Operator => ForOperator(),
        _ => Array.Empty<string>(),
    };
}
