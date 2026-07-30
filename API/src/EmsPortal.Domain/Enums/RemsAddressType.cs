namespace EmsPortal.Domain.Enums;

/// <summary>
/// The kind of address recorded for a REMS entity (REMS WO-110). A fixed pair (not an option set).
/// Stored as a string in the database (<c>HasConversion&lt;string&gt;</c>).
/// </summary>
public enum RemsAddressType
{
    /// <summary>Physical / street address.</summary>
    Physical,

    /// <summary>Mailing address (used when it differs from the physical address).</summary>
    Mailing,
}
