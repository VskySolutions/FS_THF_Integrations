namespace EmsPortal.Domain.Enums;

/// <summary>
/// The role an approver plays on a REMS approval task (REMS WO-110). Stored as a string in the
/// database (<c>HasConversion&lt;string&gt;</c>).
/// <para>
/// <c>ManagingShareholder</c> stood between DepartmentDirector and CommissionRecipient, and the migration
/// that retired it rewrote the tasks carrying the role to <see cref="Approver"/>, so no stored value is
/// orphaned. <see cref="Shareholder"/> took over what it was for — signing off on every engagement the firm
/// routes — without the part that made it a seat: any number of people can hold it, and the tasks say which
/// of them approved.
/// </para>
/// </summary>
public enum RemsApproverRole
{
    /// <summary>Client Service Executive.</summary>
    CSE,

    /// <summary>Director of the owning department.</summary>
    DepartmentDirector,

    /// <summary>A recipient of a commission split who must approve their share.</summary>
    CommissionRecipient,

    /// <summary>
    /// A shareholder of the firm — a holder of the <c>Shareholder</c> role, on every engagement's approver
    /// list by standing and not removable from it. Reviews on the same terms as a plain
    /// <see cref="Approver"/>, checklist and all: what the role changes is that they are always asked, not
    /// what they are asked. The fee estimate and realization stay with the Department Director.
    /// </summary>
    Shareholder,

    /// <summary>
    /// A hand-picked approver with no standing of their own on the engagement — someone added on the
    /// Approval tab who is not a shareholder, the CSE, the department director or a commission recipient.
    /// Reviews the engagement without seeing the fee estimate or realization, which stay reserved to the
    /// director.
    /// </summary>
    Approver,
}
