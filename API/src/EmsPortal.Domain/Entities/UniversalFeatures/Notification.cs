using EmsPortal.Domain.Enums;

namespace EmsPortal.Domain.Entities;

/// <summary>
/// An in-app notification for a single user. Created by <c>NotificationDispatcher</c>, which also
/// fans out to email (best-effort). May optionally deep-link to an entity record.
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

/// <summary>A user's per-channel delivery preference for one notification type.</summary>
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

    /// <summary>Deliver via email.</summary>
    public bool Email { get; set; } = true;
}
