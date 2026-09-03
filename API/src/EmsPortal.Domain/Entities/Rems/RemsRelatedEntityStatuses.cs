namespace EmsPortal.Domain.Entities;

/// <summary>
/// The <c>REMS.RelatedEntityStatus</c> option-set codes — how far a client's RELATED client has got.
/// <para>
/// A related client is somebody the intake form declared alongside the client themselves: another person
/// on an individual's return (<see cref="REMSAdditionalIndividual"/>) or another business a company named
/// (<see cref="REMSAdditionalEntity"/>). Both rows carry a <c>RelatedStatusId</c> pointing at one of
/// these, and the Related Entities list is where it is read and set.
/// </para>
/// <para>
/// THE STATUS IS SET BY HAND. Nothing in the workflow advances it: raising the follow-up request does
/// not, approving that request does not, and neither does the parent request's own status. It is a note
/// the firm keeps about work that mostly happens outside this portal, and the whole value of it is that
/// whoever is doing that work says where it has got to. The application writes exactly one of these
/// values, <see cref="NotInitiated"/>, and only as the default a row starts life at.
/// </para>
/// <para>
/// Which is why the list is OPEN rather than closed (see <c>DefaultOptionSets</c>): a firm that tracks a
/// fifth state — declined, not applicable, on hold — can add it and every row can be set to it, because
/// nothing here branches on the set of values. The four seeded codes are locked against deletion and
/// re-coding all the same, since <see cref="NotInitiated"/> is the one the server writes.
/// </para>
/// </summary>
public static class RemsRelatedEntityStatuses
{
    /// <summary>
    /// Nothing has been raised for this related client yet — the state every declared row starts in, and
    /// the only one the application itself ever writes.
    /// <para>
    /// It is also what a row holding NO status reads as: the column is nullable so that adding it did not
    /// have to invent an answer for every row already on file, and null has always meant "nobody has said
    /// yet", which is this value.
    /// </para>
    /// </summary>
    public const string NotInitiated = "not_initiated";

    /// <summary>A REMS request has been raised for this related client and is being worked.</summary>
    public const string RemsInitiated = "rems_initiated";

    /// <summary>Their request has reached the approvers.</summary>
    public const string PendingApproval = "pending_approval";

    /// <summary>Their engagement is approved — the end of the road for this row.</summary>
    public const string Approved = "approved";

    /// <summary>
    /// Whether a row has been taken past <see cref="NotInitiated"/>. The list draws a row's reference
    /// (<c>REMS-1042-C1</c>) only for these: before anything is raised there is nothing for the reference
    /// to point at, and printing one anyway invites somebody to go looking for a request that does not
    /// exist.
    /// </summary>
    public static bool IsUnderway(string? status)
        => !string.IsNullOrWhiteSpace(status) && status != NotInitiated;
}
