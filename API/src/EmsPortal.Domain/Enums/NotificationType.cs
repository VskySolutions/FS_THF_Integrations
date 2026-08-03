namespace EmsPortal.Domain.Enums;

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

    /// <summary>A generic system notification not covered by a more specific type.</summary>
    System = 6,

    /// <summary>A REMS request was assigned to the user (in-app only — no email template, REMS WO-111).</summary>
    RemsRequestAssigned = 7,

    /// <summary>The user was assigned as the CSE on a REMS request/form (in-app only — no email template, REMS WO-112).</summary>
    RemsCseAssigned = 8,

    /// <summary>A REMS onboarding form was emailed to the customer (in-app only — no email template, REMS WO-112).</summary>
    RemsFormSent = 9,

    /// <summary>A customer submitted their REMS onboarding form (in-app only; the external email goes via IRemsEmailNotifier, REMS WO-113).</summary>
    RemsFormSubmitted = 10,

    /// <summary>An approver was asked to act on a REMS approval task (in-app only — no email template, REMS WO-114).</summary>
    RemsApprovalRequested = 11,

    /// <summary>A REMS engagement was fully approved (in-app only — no email template, REMS WO-114).</summary>
    RemsEngagementApproved = 12,

    /// <summary>A REMS engagement approval round was rejected (in-app only — no email template, REMS WO-114).</summary>
    RemsEngagementRejected = 13,

    /// <summary>A rejected REMS engagement was resubmitted for approval (in-app only — no email template, REMS WO-114).</summary>
    RemsApprovalResubmitted = 14,

    /// <summary>A REMS request was submitted to the pool and is waiting to be picked up — sent to the tenant's admins (in-app only).</summary>
    RemsRequestSubmitted = 15,

    /// <summary>An admin picked up the requester's REMS request — sent back to whoever raised it (in-app only).</summary>
    RemsRequestPickedUp = 16,
}
