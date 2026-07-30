namespace EmsPortal.Shared.Security;

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

    // Persons (CRM master records — the precursor to a login account)
    public const string PersonsRead = "persons.read";
    public const string PersonsWrite = "persons.write";
    public const string PersonsDelete = "persons.delete";

    // Users
    public const string UsersRead = "users.read";
    public const string UsersWrite = "users.write";
    public const string UsersResetPassword = "users.reset_password";
    /// <summary>Create user groups and assign/remove users from them (and delete groups).</summary>
    public const string UsersGroupManagement = "users.groupManagement";

    // Roles (RBAC management)
    public const string RolesRead = "roles.read";
    public const string RolesWrite = "roles.write";
    public const string RolesAssign = "roles.assign";

    // Permission Groups (RBAC composition layer)
    /// <summary>Create, edit, and compose Permission Groups (and compose them into roles).</summary>
    public const string GroupsManage = "groups.manage";

    // SMTP Email Accounts
    /// <summary>Create, edit, delete, set-active, and test-send SMTP email accounts. Reads require only <see cref="UsersRead"/>.</summary>
    public const string EmailManage = "email.manage";

    // Universal Features (Phase 14)
    /// <summary>Manage tenant-wide Universal Feature settings: tags, shared saved views, tenant sticky
    /// notes, and Modified Log field configuration.</summary>
    public const string SettingsManage = "settings.manage";
    /// <summary>View, restore, and permanently delete soft-deleted records (Deleted Records Management).</summary>
    public const string RecordsAdminDelete = "records.adminDelete";

    // Option Sets (tenant-configurable input value lists)
    /// <summary>Read option lists and their values (for pickers and the management UI).</summary>
    public const string OptionSetsRead = "optionSets.read";
    /// <summary>Create, edit, reorder, and delete a tenant's own option lists and values.</summary>
    public const string OptionSetsManage = "optionSets.manage";

    /// <summary>Every defined permission key.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        TenantsRead, TenantsWrite, TenantsArchive,
        PersonsRead, PersonsWrite, PersonsDelete,
        UsersRead, UsersWrite, UsersResetPassword, UsersGroupManagement,
        RolesRead, RolesWrite, RolesAssign,
        GroupsManage,
        EmailManage,
        SettingsManage, RecordsAdminDelete,
        OptionSetsRead, OptionSetsManage
    };

    /// <summary>Permission sets for the seeded system roles.</summary>
    public static IReadOnlyList<string> ForSuperAdmin() => All;

    public static IReadOnlyList<string> ForTenantAdmin() => new[]
    {
        TenantsRead,
        // Deleting persons and changing role assignments are Super-Admin-only (PersonsDelete /
        // RolesAssign intentionally excluded here).
        PersonsRead, PersonsWrite,
        UsersRead, UsersWrite, UsersResetPassword, UsersGroupManagement,
        RolesRead,
        // Tenant Admins manage Permission Groups within their own tenant.
        GroupsManage,
        // Tenant Admins manage their tenant's SMTP email accounts.
        EmailManage,
        // Tenant Admins manage tenant-wide UF settings and the deleted-records lifecycle.
        SettingsManage, RecordsAdminDelete,
        // Tenant Admins manage their tenant's option lists.
        OptionSetsRead, OptionSetsManage
    };

    /// <summary>
    /// The seeded permission set for a system role name (SuperAdmin/TenantAdmin), or an empty set
    /// for any other name (including custom roles). Used as the fallback when a caller carries only a
    /// role claim (API-key callers, pre-RBAC tokens) and no explicit permission claims.
    /// </summary>
    public static IReadOnlyList<string> ForSystemRole(string? roleName) => roleName switch
    {
        Roles.SuperAdmin => ForSuperAdmin(),
        Roles.TenantAdmin => ForTenantAdmin(),
        _ => Array.Empty<string>(),
    };
}
