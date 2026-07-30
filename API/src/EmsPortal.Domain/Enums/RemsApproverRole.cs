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
}
