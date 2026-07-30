namespace EmsPortal.Domain.Enums;

/// <summary>
/// Identifies a transactional email template. Each key has a seeded platform-default template
/// (global) which a tenant may override. Stored as a string for stability across releases.
/// </summary>
public enum EmailTemplateKey
{
    /// <summary>Sent when a Person is promoted to a user account; carries the temporary password and login link.</summary>
    UserInvitation = 0,

    /// <summary>Sent when an administrator resets a user's password; carries the new temporary password.</summary>
    PasswordReset = 1,

    /// <summary>Confirmation sent after a user changes their own password.</summary>
    PasswordChanged = 2,

    /// <summary>General welcome message sent when an account becomes active.</summary>
    Welcome = 3,

    /// <summary>A user was @mentioned in a note (Universal Features). Sent to the mentioned user.</summary>
    MentionReceived = 11,

    /// <summary>A reminder a user set has reached its due time (Universal Features). Sent to the owner.</summary>
    ReminderDue = 12,
}
