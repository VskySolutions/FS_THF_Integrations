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

    // REMS — Real Estate Management System (WO-110+). The operational roles Partner/Admin/Approver
    // are composed of these keys (see ForPartner/ForAdmin/ForApprover).
    /// <summary>View REMS requests.</summary>
    public const string RemsRequestsRead = "rems.requests.read";
    /// <summary>Create REMS requests.</summary>
    public const string RemsRequestsCreate = "rems.requests.create";
    /// <summary>Edit REMS requests.</summary>
    public const string RemsRequestsUpdate = "rems.requests.update";
    /// <summary>Delete REMS requests.</summary>
    public const string RemsRequestsDelete = "rems.requests.delete";
    /// <summary>Assign a REMS request to a person/pool.</summary>
    public const string RemsRequestsAssign = "rems.requests.assign";
    /// <summary>View the shared REMS request pool.</summary>
    public const string RemsPoolRead = "rems.pool.read";
    /// <summary>Create, edit, and configure REMS forms.</summary>
    public const string RemsFormsManage = "rems.forms.manage";
    /// <summary>Send REMS forms to recipients.</summary>
    public const string RemsFormsSend = "rems.forms.send";
    /// <summary>Create and manage REMS engagements.</summary>
    public const string RemsEngagementsManage = "rems.engagements.manage";
    /// <summary>Initiate/route a REMS approval round.</summary>
    public const string RemsApprovalsSend = "rems.approvals.send";
    /// <summary>Act on (approve/reject) a REMS approval task assigned to the caller. Record-scoped.</summary>
    public const string RemsApprovalsAct = "rems.approvals.act";
    /// <summary>Read the REMS email log.</summary>
    public const string RemsEmailLogRead = "rems.emailLog.read";

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
        OptionSetsRead, OptionSetsManage,
        RemsRequestsRead, RemsRequestsCreate, RemsRequestsUpdate, RemsRequestsDelete, RemsRequestsAssign,
        RemsPoolRead, RemsFormsManage, RemsFormsSend, RemsEngagementsManage,
        RemsApprovalsSend, RemsApprovalsAct, RemsEmailLogRead
    };

    /// <summary>Permission sets for the seeded system roles.</summary>
    public static IReadOnlyList<string> ForSuperAdmin() => All;

    public static IReadOnlyList<string> ForTenantAdmin() => new[]
    {
        TenantsRead,
        // Deleting persons stays Super-Admin-only (PersonsDelete intentionally excluded here).
        PersonsRead, PersonsWrite,
        UsersRead, UsersWrite, UsersResetPassword, UsersGroupManagement,
        // Tenant Admins manage the roles of users in their OWN tenant. The permission alone is not the
        // whole boundary: UsersController confines them to their active tenant, refuses to grant the
        // Super Admin role, and refuses a Super Admin target.
        RolesRead, RolesAssign,
        // Tenant Admins manage Permission Groups within their own tenant.
        GroupsManage,
        // Tenant Admins manage their tenant's SMTP email accounts.
        EmailManage,
        // Tenant Admins manage tenant-wide UF settings and the deleted-records lifecycle.
        SettingsManage, RecordsAdminDelete,
        // Tenant Admins manage their tenant's option lists.
        OptionSetsRead, OptionSetsManage,
        // Full REMS access within their tenant — the same set a REMS Admin holds, plus the approver's own
        // act permission. Tenant isolation still applies: this widens WHAT they can do in their tenant,
        // never WHICH tenant. RemsApprovalsAct stays record-scoped to tasks assigned to them.
        RemsRequestsRead, RemsRequestsCreate, RemsRequestsUpdate, RemsRequestsDelete, RemsRequestsAssign,
        RemsPoolRead, RemsFormsManage, RemsFormsSend, RemsEngagementsManage,
        RemsApprovalsSend, RemsApprovalsAct, RemsEmailLogRead
    };

    /// <summary>REMS Partner: works their own requests (read/create/update) and assigns them.</summary>
    public static IReadOnlyList<string> ForPartner() => new[]
    {
        RemsRequestsRead, RemsRequestsCreate, RemsRequestsUpdate, RemsRequestsAssign, OptionSetsRead
    };

    /// <summary>
    /// REMS Admin: full request lifecycle plus pool, forms, engagements, approvals routing and the email log
    /// — and acting on approval tasks of their own.
    /// <para>
    /// <see cref="RemsApprovalsAct"/> is not optional here: an Admin is routinely made an APPROVER by the
    /// normal flow. The CSE is picked from the tenant's Admins on the Build-EMS screen and is an automatic
    /// approver on every engagement of that request, and commission recipients (also automatic approvers)
    /// come from the CSE user group. Without this key such a user gets a pending task they can neither see
    /// nor decide, and since a round completes only when EVERY task is approved, the engagement would sit in
    /// PendingApproval indefinitely. As on <see cref="ForTenantAdmin"/>, the key stays record-scoped: every
    /// approval endpoint additionally requires <c>ApproverId == caller</c> and 404s otherwise, so this grants
    /// no visibility of anyone else's tasks.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> ForAdmin() => new[]
    {
        RemsRequestsRead, RemsRequestsCreate, RemsRequestsUpdate, RemsRequestsDelete, RemsRequestsAssign,
        RemsPoolRead, RemsFormsManage, RemsFormsSend, RemsEngagementsManage,
        RemsApprovalsSend, RemsApprovalsAct, RemsEmailLogRead, OptionSetsRead
    };

    /// <summary>
    /// REMS Approver: may act on approval tasks. Visibility is record-scoped to the caller's assigned
    /// REMSApprovalTask rows (enforced in the REMS query layer), not tenant-wide.
    /// </summary>
    public static IReadOnlyList<string> ForApprover() => new[] { RemsApprovalsAct };

    /// <summary>
    /// The seeded permission set for a system role name (SuperAdmin/TenantAdmin and the REMS operational
    /// roles Partner/Admin/Approver), or an empty set for any other name (including custom roles). Used
    /// as the fallback when an assignment carries no explicit permission keys and when a caller holds
    /// only a role claim (API-key callers, pre-RBAC tokens).
    /// </summary>
    public static IReadOnlyList<string> ForSystemRole(string? roleName) => roleName switch
    {
        Roles.SuperAdmin => ForSuperAdmin(),
        Roles.TenantAdmin => ForTenantAdmin(),
        Roles.Partner => ForPartner(),
        Roles.Admin => ForAdmin(),
        Roles.Approver => ForApprover(),
        _ => Array.Empty<string>(),
    };
}
