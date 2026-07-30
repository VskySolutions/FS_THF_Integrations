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
    /// Record-scoped approver. The role grants only <c>rems.approvals.act</c>; an Approver's visibility
    /// is further restricted to the REMSApprovalTask rows assigned to them (enforced in the REMS query
    /// layer, WO-111/112/114) rather than tenant-wide.
    /// </summary>
    public const string Approver = "Approver";
}
