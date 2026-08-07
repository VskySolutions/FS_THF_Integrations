namespace EmsPortal.Domain.Enums;

/// <summary>
/// Approval lifecycle of a REMS engagement (REMS WO-110). Stored as a string in the database
/// (<c>HasConversion&lt;string&gt;</c>).
/// </summary>
public enum RemsEngagementStatus
{
    /// <summary>Being prepared; not yet routed for approval.</summary>
    Draft,

    /// <summary>Submitted and awaiting approver decisions.</summary>
    PendingApproval,

    /// <summary>Rejected by an approver; requires rework and resubmission.</summary>
    Rejected,

    /// <summary>Approved by all required approvers.</summary>
    Approved,
}
