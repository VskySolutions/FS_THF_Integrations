using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Security;
using IntegrationHub.Application.Abstractions.UniversalFeatures;
using IntegrationHub.Domain.Entities;

namespace IntegrationHub.Application.UniversalFeatures;

/// <summary>
/// Default <see cref="IActivityEventWriter"/>: stages an append-only <see cref="ActivityEvent"/> on the
/// current unit of work so it commits atomically with the triggering change (Universal Features ADR-002).
/// The tenant id is stamped by the DbContext; the actor defaults to the current authenticated user.
/// </summary>
public sealed class ActivityEventWriter : IActivityEventWriter
{
    private readonly IActivityEventRepository _events;
    private readonly IActorAccessor _actorAccessor;

    public ActivityEventWriter(IActivityEventRepository events, IActorAccessor actorAccessor)
    {
        _events = events;
        _actorAccessor = actorAccessor;
    }

    public Task WriteAsync(CreateActivityEventDto activityEvent, CancellationToken cancellationToken = default)
    {
        var actorId = activityEvent.ActorId
            ?? (Guid.TryParse(_actorAccessor.GetCurrentActor(), out var id) ? id : (Guid?)null);

        return _events.AddAsync(new ActivityEvent
        {
            Id = Guid.NewGuid(),
            EntityType = activityEvent.EntityType,
            EntityId = activityEvent.EntityId,
            EventType = activityEvent.EventType,
            OldValue = activityEvent.OldValue,
            NewValue = activityEvent.NewValue,
            ActorId = actorId,
        }, cancellationToken);
    }
}
