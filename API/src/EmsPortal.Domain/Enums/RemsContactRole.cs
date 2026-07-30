namespace EmsPortal.Domain.Enums;

/// <summary>
/// The role a person plays as a contact on a REMS entity (REMS WO-110). A known set that varies by
/// industry group; persisted as a free string code on <c>REMSEntityContact.ContactRole</c> rather
/// than an enum column, so this type documents the canonical values used by the application.
/// </summary>
public enum RemsContactRole
{
    /// <summary>The individual themselves (Individual industry group).</summary>
    Self,

    /// <summary>The individual's spouse.</summary>
    Spouse,

    /// <summary>Chief Executive Officer (Business industry group).</summary>
    CEO,

    /// <summary>Chief Financial Officer (Business industry group).</summary>
    CFO,

    /// <summary>Accounts Payable contact.</summary>
    AccountsPayable,

    /// <summary>Banking contact.</summary>
    Banker,

    /// <summary>Legal contact.</summary>
    Lawyer,

    /// <summary>Finance Director (Government industry group).</summary>
    FinanceDirector,
}
