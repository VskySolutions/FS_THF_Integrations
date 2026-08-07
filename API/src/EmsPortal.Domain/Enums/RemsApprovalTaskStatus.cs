namespace EmsPortal.Domain.Enums;

/// <summary>
/// Decision state of a single approver's task within a REMS approval round (REMS WO-110). Stored as
/// a string in the database (<c>HasConversion&lt;string&gt;</c>).
/// </summary>
public enum RemsApprovalTaskStatus
{
    /// <summary>Awaiting the approver's decision.</summary>
    Pending,

    /// <summary>The approver approved.</summary>
    Approved,

    /// <summary>The approver rejected.</summary>
    Rejected,
}
