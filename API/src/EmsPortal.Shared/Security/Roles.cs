namespace EmsPortal.Shared.Security;

/// <summary>
/// The platform system roles enforced by RBAC (SuperAdmin &gt; TenantAdmin) plus the seeded REMS
/// operational roles (Partner/Admin/Approver). All other access is governed by custom,
/// permission-based roles.
/// </summary>
public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string TenantAdmin = "TenantAdmin";

    // REMS operational roles (WO-122). Seeded system roles carrying the REMS permission sets; assigned
    // per (user, tenant) alongside any other roles (multi-role assignments).
    public const string Partner = "Partner";
    public const string Admin = "Admin";

    /// <summary>
    /// Marks a user as offerable in the REMS "add approvers" picker. It grants no permissions: deciding an
    /// approval task is authorised by owning the task, not by holding this role — the CSE and every
    /// commission recipient approve without it, whatever roles they hold. See
    /// <see cref="Permissions.ForApprover"/>.
    /// </summary>
    public const string Approver = "Approver";
}
