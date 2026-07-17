using EmsPortal.Domain.Enums;

namespace EmsPortal.Api.Models.UniversalFeatures;

/// <summary>An activity-timeline event as returned to the client.</summary>
public sealed record ActivityEventResponse(
    Guid Id,
    EntityType EntityType,
    Guid EntityId,
    string EventType,
    Guid? ActorId,
    string ActorName,
    string? OldValue,
    string? NewValue,
    DateTime OccurredOnUtc);
