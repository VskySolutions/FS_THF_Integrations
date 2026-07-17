using EmsPortal.Domain.Enums;

namespace EmsPortal.Domain.Entities;

/// <summary>
/// A personal, date-based reminder a user sets against an entity record. Dispatched (in-app +
/// email) by the recurring <c>ReminderDispatchJob</c> when <see cref="DueAtUtc"/> passes.
/// </summary>
public class Reminder : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The kind of entity the reminder is attached to.</summary>
    public EntityType EntityType { get; set; }

    /// <summary>The id of the entity the reminder is attached to.</summary>
    public Guid EntityId { get; set; }

    /// <summary>The user who owns the reminder (and receives the dispatch).</summary>
    public Guid UserId { get; set; }

    /// <summary>When the reminder is due (UTC).</summary>
    public DateTime DueAtUtc { get; set; }

    /// <summary>Optional free-text note shown with the reminder.</summary>
    public string? Note { get; set; }

    /// <summary>True once the dispatch job has fired the notification + email.</summary>
    public bool IsDispatched { get; set; }

    /// <summary>True once the reminder is past due and still un-actioned.</summary>
    public bool IsOverdue { get; set; }
}
