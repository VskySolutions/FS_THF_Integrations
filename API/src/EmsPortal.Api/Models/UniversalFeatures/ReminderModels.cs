using EmsPortal.Domain.Enums;

namespace EmsPortal.Api.Models.UniversalFeatures;

/// <summary>Request to create a reminder on an entity record.</summary>
public sealed class CreateReminderRequest
{
    public EntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public DateTime DueAtUtc { get; set; }
    public string? Note { get; set; }
}

/// <summary>Request to edit a reminder's due date or note.</summary>
public sealed class UpdateReminderRequest
{
    public DateTime DueAtUtc { get; set; }
    public string? Note { get; set; }
}

/// <summary>A reminder as returned to the client.</summary>
public sealed record ReminderResponse(
    Guid Id,
    EntityType EntityType,
    Guid EntityId,
    DateTime DueAtUtc,
    string? Note,
    bool IsDispatched,
    bool IsOverdue,
    DateTime CreatedOnUtc);
