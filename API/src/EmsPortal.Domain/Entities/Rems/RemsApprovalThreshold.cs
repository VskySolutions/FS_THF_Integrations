namespace EmsPortal.Domain.Entities;

/// <summary>
/// How many approvers have to decline before a REMS approval round fails.
/// <para>
/// A round used to die on the FIRST rejection. It now survives a single decline and closes on the second,
/// which means a lone objector is outvoted rather than decisive — deliberate, but worth knowing: one
/// "no" against four "yes" no longer stops an engagement being approved.
/// </para>
/// </summary>
public static class RemsApprovalThreshold
{
    /// <summary>Declines needed to close a round.</summary>
    public const int Declines = 2;

    /// <summary>
    /// The threshold that actually applies to a round of <paramref name="approverCount"/> approvers,
    /// never more than there are people to reach it. Without this a round with a single approver could
    /// never fail: their decline would leave the count at one, short of the threshold forever, and the
    /// round would sit open with no pending tasks left to move it.
    /// </summary>
    public static int EffectiveFor(int approverCount) =>
        approverCount < 1 ? 1 : Math.Min(Declines, approverCount);
}
