namespace EmsPortal.Domain.Entities;

/// <summary>
/// How many approvers have to decline before a REMS approval round fails.
/// <para>
/// One is enough: any single decline closes the round there and then. A round briefly survived a lone
/// objection and closed on the second decline — that was dropped, because an approver who says no has to
/// be answered rather than outvoted by the approvals sitting beside them.
/// </para>
/// <para>
/// Closing the round is the whole of it: the rework comes back through a NEW round, so every approver —
/// including the ones who had already approved the round that failed — decides again and works their
/// checklist again from blank. Nothing carries over.
/// </para>
/// </summary>
public static class RemsApprovalThreshold
{
    /// <summary>Declines needed to close a round.</summary>
    public const int Declines = 1;

    /// <summary>
    /// The threshold that actually applies to a round of <paramref name="approverCount"/> approvers, never
    /// more than there are people to reach it. At <see cref="Declines"/> = 1 the cap never bites; it is
    /// what stops a RAISED threshold stranding a small round — a round of one approver needing two declines
    /// could never fail, and would sit open forever with no pending task left to move it.
    /// </summary>
    public static int EffectiveFor(int approverCount) =>
        approverCount < 1 ? 1 : Math.Min(Declines, approverCount);
}
