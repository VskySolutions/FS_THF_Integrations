using EmsPortal.Domain.Enums;

namespace EmsPortal.Domain.Entities;

/// <summary>
/// An in-app notification for a single user. Created by <c>NotificationDispatcher</c> (in-app only since
/// WO-124 — notifications are never emailed). May optionally deep-link to an entity record.
/// </summary>
public class Notification : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The recipient user.</summary>
    public Guid UserId { get; set; }

    /// <summary>The notification category.</summary>
    public NotificationType Type { get; set; }

    /// <summary>Short headline.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Body / detail text.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Optional entity the notification links to.</summary>
    public EntityType? EntityType { get; set; }

    /// <summary>Optional id of the linked entity.</summary>
    public Guid? EntityId { get; set; }

    /// <summary>True once the user has read it.</summary>
    public bool IsRead { get; set; }

    /// <summary>True when this notification was folded into a group within the dedupe window.</summary>
    public bool IsGrouped { get; set; }
}

/// <summary>
/// A user's delivery preference for one notification type. Only the in-app channel is configurable since
/// WO-124 (AC-UNI-013.2); notification types are in-app only and never emailed.
/// </summary>
public class NotificationPreference : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The user the preference belongs to.</summary>
    public Guid UserId { get; set; }

    /// <summary>The notification type this preference row configures.</summary>
    public NotificationType NotificationType { get; set; }

    /// <summary>Deliver to the in-app notification centre.</summary>
    public bool InApp { get; set; } = true;

    /// <summary>Retained column, no longer used (WO-124): notification types are in-app only, never emailed.</summary>
    public bool Email { get; set; }
}
