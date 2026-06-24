namespace IntegrationHub.Domain.Enums;

/// <summary>
/// Categories of system notification. Drives the per-user notification preference matrix
/// (in-app / email channels) and the notification list type filter.
/// </summary>
public enum NotificationType
{
    /// <summary>The user was @mentioned in a note.</summary>
    Mention = 1,

    /// <summary>A reminder the user set has reached its due time.</summary>
    ReminderDue = 2,

    /// <summary>A Customer Request the user is involved with changed status.</summary>
    CustomerStatusChanged = 3,

    /// <summary>An integration / sync the user triggered completed successfully.</summary>
    SyncCompleted = 4,

    /// <summary>An integration / sync the user triggered failed.</summary>
    SyncFailed = 5,

    /// <summary>A generic system notification not covered by a more specific type.</summary>
    System = 6,
}
