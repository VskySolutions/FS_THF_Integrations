namespace EmsPortal.Domain.Enums;

/// <summary>
/// The role an approver plays on a REMS approval task (REMS WO-110). Stored as a string in the
/// database (<c>HasConversion&lt;string&gt;</c>).
/// </summary>
public enum RemsApproverRole
{
    /// <summary>Client Service Executive.</summary>
    CSE,

    /// <summary>Director of the owning department.</summary>
    DepartmentDirector,

    /// <summary>Managing shareholder sign-off.</summary>
    ManagingShareholder,

    /// <summary>A recipient of a commission split who must approve their share.</summary>
    CommissionRecipient,

    /// <summary>
    /// A hand-picked approver with no other standing on the engagement — someone added on the Approval
    /// tab who is not the CSE, the department director, the managing shareholder or a commission
    /// recipient. Reviews the engagement without seeing the fee estimate or realization, which stay
    /// reserved to the director and managing shareholder.
    /// </summary>
    Approver,
}
