using EmsPortal.Domain.Enums;

namespace EmsPortal.Application.Abstractions.UniversalFeatures;

/// <summary>
/// The payload for recording one activity-timeline event (Universal Features ADR-002).
/// </summary>
/// <param name="EntityType">The kind of entity the event is attached to.</param>
/// <param name="EntityId">The id of the entity the event is attached to.</param>
/// <param name="EventType">The event kind — see <see cref="Domain.Entities.ActivityEventTypes"/>.</param>
/// <param name="OldValue">Optional previous value for change-style events.</param>
/// <param name="NewValue">Optional new value for change-style events.</param>
/// <param name="ActorId">Optional explicit actor; when null the current actor is resolved automatically.</param>
public sealed record CreateActivityEventDto(
    EntityType EntityType,
    Guid EntityId,
    string EventType,
    string? OldValue = null,
    string? NewValue = null,
    Guid? ActorId = null);

/// <summary>
/// Cross-cutting, in-process writer for the user-facing activity timeline. Called synchronously by
/// any module whenever a tracked action occurs; the event is staged on the current unit of work and
/// committed atomically with the triggering change (ADR-002). It is the only writer of
/// <see cref="Domain.Entities.ActivityEvent"/> — the API exposes a read endpoint only.
/// </summary>
public interface IActivityEventWriter
{
    /// <summary>Stages an append-only activity event on the current unit of work.</summary>
    Task WriteAsync(CreateActivityEventDto activityEvent, CancellationToken cancellationToken = default);
}
