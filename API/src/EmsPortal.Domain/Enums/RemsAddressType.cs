namespace EmsPortal.Domain.Enums;

/// <summary>
/// The kind of address recorded for a REMS entity (REMS WO-110). A fixed set (not an option set).
/// Stored as a string in the database (<c>HasConversion&lt;string&gt;</c>).
/// <para>
/// Every address the client gives is written. They fill the physical address and may copy it forward into
/// the mailing address, and either of those forward into a billing address — but a copy is a snapshot
/// taken once, not a live mirror, so each row stands on its own afterwards. That is deliberate: the old
/// "mailing differs = false" wrote no mailing row at all, which quietly moved the mailing address every
/// time the physical one was corrected.
/// </para>
/// </summary>
public enum RemsAddressType
{
    /// <summary>Physical / street address. The one the others are usually copied from.</summary>
    Physical,

    /// <summary>Mailing address.</summary>
    Mailing,

    /// <summary>
    /// Billing address. Held here rather than on the client, so every address shares one shape — and the
    /// only type an entity may have SEVERAL of, because a client invoiced at two offices has two, each
    /// addressed to a different person.
    /// </summary>
    Billing,
}
