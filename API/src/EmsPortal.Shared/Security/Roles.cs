namespace EmsPortal.Shared.Security;

/// <summary>
/// The platform system roles enforced by RBAC (SuperAdmin &gt; TenantAdmin), the seeded REMS operational
/// roles (Partner/Admin), and the four REMS SEAT roles a firm fills its engagements from. All other
/// access is governed by custom, permission-based roles.
/// </summary>
public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string TenantAdmin = "TenantAdmin";

    // REMS operational roles (WO-122). Seeded system roles carrying the REMS permission sets; assigned
    // per (user, tenant) alongside any other roles (multi-role assignments).
    public const string Partner = "Partner";
    public const string Admin = "Admin";

    // ---- REMS seat roles ----
    //
    // The four seats an engagement names. Each was a USER GROUP of the same name, created by hand per
    // tenant and looked up by that name in code; they are roles now, because that is what they always
    // were. A group answers "who is in this list"; these answer "what is this person to the firm", which
    // is the question a role is for — and it puts them in the one place a user's standing is already
    // maintained, beside Partner and Admin on the user's own page, rather than in a second list somebody
    // has to remember to keep in step.
    //
    // Like Approver before them they grant NOTHING (see Permissions.ForSeatRole). Holding one makes a
    // person offerable in the picker that fills that seat; what they may then do comes from the seat
    // itself — a CSE approves because the engagement names them its CSE, not because of a permission key.
    //
    // The names carry spaces because they are what the picker shows, and the roles UI renders a role by
    // its name. They match the groups they replace exactly, so a firm reads the same words afterwards.
    public const string Cse = "CSE";
    public const string EngagementExecutive = "Engagement Executive";
    public const string BillingManager = "Billing Manager";

    // Managing Shareholder stood here. It was a seat and a signature: whoever held it was added to EVERY
    // approval round the firm routed, on top of the CSE, the department director and the commission
    // recipients. The firm does not work that way — an engagement is signed off by the people it names —
    // so the seat is gone, and with it the tenant-wide setting behind it. Anyone whose signature a
    // particular engagement genuinely needs is added on its Approval tab, which offers everyone.

    // Approver stood here. It marked somebody as offerable in the REMS "add approvers" picker, and that
    // picker now offers every user in the tenant — an engagement can need a signature from anyone, and
    // maintaining a role to say so was a gate that only ever got in the way. Deciding an approval task
    // never needed it: that is authorised by OWNING the task.
}
