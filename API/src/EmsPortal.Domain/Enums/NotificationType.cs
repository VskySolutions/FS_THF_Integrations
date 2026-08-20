namespace EmsPortal.Domain.Enums;

/// <summary>
/// Categories of system notification. Drives the per-user notification preference matrix
/// (in-app / email channels) and the notification list type filter.
/// <para>
/// EVERY MEMBER HERE IS DISPATCHED SOMEWHERE. The preference matrix is built from
/// <c>Enum.GetValues&lt;NotificationType&gt;()</c>, so a declared-but-never-sent type shows the user a switch
/// over something that cannot arrive. Adding a member is therefore part of writing the code that sends it,
/// not a step before it.
/// </para>
/// <para>
/// RETIRED NUMBERS — never reuse: <c>3</c>, <c>4</c> and <c>5</c> (sync notifications, from an integration
/// that no longer exists), <c>6</c> (a generic "System" catch-all nothing ever raised) and <c>14</c>
/// (RemsApprovalResubmitted — a resubmitted round notifies under <see cref="RemsApprovalRequested"/> with a
/// different title, so this never fired). Values are persisted as ints on Notifications and
/// NotificationPreferences; reusing one would silently relabel whatever rows still carry it.
/// </para>
/// </summary>
public enum NotificationType
{
    /// <summary>The user was @mentioned in a note.</summary>
    Mention = 1,

    /// <summary>A reminder the user set has reached its due time.</summary>
    ReminderDue = 2,

    /// <summary>
    /// A REMS request is waiting for an admin to pick it up: the client's answers have landed on a request
    /// nobody has claimed, so this goes to EVERY admin in the tenant (in-app only — no email template).
    /// Minted for "a REMS request was assigned to you", back when an initiator named one admin at intake;
    /// nobody is named at intake now, and the number is kept because rows carry it.
    /// </summary>
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

    /// <summary>
    /// An admin sent a REMS request back to its initiator for engagement-setup rework, with their reason
    /// (in-app only). Named for the pool submission it originally announced; the pool went with Phase 16,
    /// and the number is kept because rows carry it.
    /// </summary>
    RemsRequestSubmitted = 15,

    /// <summary>
    /// The reviewing admin on a REMS request changed hands, or the initiator handed revised setup back for
    /// confirmation (in-app only). Two moves under one number for the same reason as
    /// <see cref="RemsRequestSubmitted"/> above.
    /// </summary>
    RemsRequestPickedUp = 16,
}
