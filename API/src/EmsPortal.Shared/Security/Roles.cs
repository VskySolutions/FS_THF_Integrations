namespace EmsPortal.Shared.Security;

/// <summary>
/// The platform system roles enforced by RBAC (SuperAdmin &gt; TenantAdmin), the seeded REMS operational
/// roles (Partner/Admin), and the REMS SEAT roles a firm fills its engagements from. All other access is
/// governed by custom, permission-based roles.
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
    // What a person IS to the firm, as opposed to what they may do. Three of them are seats an engagement
    // names; the fourth, Shareholder, is a standing the whole firm has. Each of the first three was a USER
    // GROUP of the same name, created by hand per tenant and looked up by that name in code; they are roles
    // now, because that is what they always were. A group answers "who is in this list"; these answer "what is this person to the firm", which
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

    /// <summary>
    /// The firm's shareholders. Unlike the three seats above, no engagement NAMES a shareholder — holding
    /// this role puts a person on every engagement's approver list automatically, on the same footing as
    /// the Department Director and the CSE: nothing writes them onto the editable list, so nothing can take
    /// them off it.
    /// <para>
    /// It replaces the single Managing Shareholder, which was a seat as well as a signature. What changes
    /// is that the signature can be shared — a firm has several shareholders, and each of them is asked —
    /// where the old seat had exactly one holder and could not tell the person apart from the post. They
    /// approve as plain approvers: the fee estimate and % realization stay with the Department Director.
    /// </para>
    /// </summary>
    public const string Shareholder = "Shareholder";

    // Approver stood here. It marked somebody as offerable in the REMS "add approvers" picker, and that
    // picker now offers every user in the tenant — an engagement can need a signature from anyone, and
    // maintaining a role to say so was a gate that only ever got in the way. Deciding an approval task
    // never needed it: that is authorised by OWNING the task.
}
