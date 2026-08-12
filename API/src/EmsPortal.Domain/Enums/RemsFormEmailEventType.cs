namespace EmsPortal.Domain.Enums;

/// <summary>
/// A delivery-tracking event reported for a REMS onboarding-form email (REMS WO-110). Rows are
/// append-only. Stored as a string in the database (<c>HasConversion&lt;string&gt;</c>).
/// </summary>
public enum RemsFormEmailEventType
{
    /// <summary>The provider accepted the message for delivery.</summary>
    Sent,

    /// <summary>The provider confirmed delivery to the recipient's mailbox.</summary>
    Delivered,

    /// <summary>The recipient opened the message.</summary>
    Opened,

    /// <summary>Delivery failed (bounce, rejection, etc.).</summary>
    Failed,

    /// <summary>
    /// A reminder was sent to a client who had not submitted yet. Distinct from <see cref="Sent"/> on
    /// purpose: the log is the record of what the client was actually sent, and "we chased them four
    /// times" is exactly the thing it should be able to answer. Stored as a string, so this value needs
    /// no migration.
    /// </summary>
    Reminder,
}
