namespace EmsPortal.Domain.Enums;

/// <summary>
/// Outcome of a single REMS approval round (REMS WO-110). A round is immutable history: a new round
/// is created on each resubmission and the row is updated once on completion. Stored as a string in
/// the database (<c>HasConversion&lt;string&gt;</c>).
/// </summary>
public enum RemsApprovalRoundStatus
{
    /// <summary>Awaiting the approvers' decisions.</summary>
    Pending,

    /// <summary>At least one approver rejected the round.</summary>
    Rejected,

    /// <summary>All approvers approved the round.</summary>
    Approved,
}
