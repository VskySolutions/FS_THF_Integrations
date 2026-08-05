using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Application.Abstractions.Security;
using EmsPortal.Application.Abstractions.UniversalFeatures;
using EmsPortal.Domain.Entities;

namespace EmsPortal.Application.UniversalFeatures;

/// <summary>
/// Default <see cref="IActivityEventWriter"/>: stages an append-only <see cref="ActivityEvent"/> on the
/// current unit of work so it commits atomically with the triggering change (Universal Features ADR-002).
/// The tenant id is stamped by the DbContext; the actor defaults to the current authenticated user.
/// </summary>
public sealed class ActivityEventWriter : IActivityEventWriter
{
    private readonly IActivityEventRepository _events;
    private readonly IAggregateRootTouch _touch;
    private readonly IActorAccessor _actorAccessor;

    public ActivityEventWriter(
        IActivityEventRepository events, IAggregateRootTouch touch, IActorAccessor actorAccessor)
    {
        _events = events;
        _touch = touch;
        _actorAccessor = actorAccessor;
    }

    public async Task WriteAsync(CreateActivityEventDto activityEvent, CancellationToken cancellationToken = default)
    {
        var actorId = activityEvent.ActorId
            ?? (Guid.TryParse(_actorAccessor.GetCurrentActor(), out var id) ? id : (Guid?)null);

        await _events.AddAsync(new ActivityEvent
        {
            Id = Guid.NewGuid(),
            EntityType = activityEvent.EntityType,
            EntityId = activityEvent.EntityId,
            EventType = activityEvent.EventType,
            OldValue = activityEvent.OldValue,
            NewValue = activityEvent.NewValue,
            ActorId = actorId,
        }, cancellationToken);

        // Bubble the change to the record the event is ABOUT. An activity event is written whenever
        // something happens to a record — including when the thing that actually changed was one of its
        // children (an engagement edited, an approval decided, a form sent). Those writes touch only the
        // child row, so without this the parent's Updated By / Updated On would still describe whatever
        // last edited the parent directly, and the lists — now ordered by UpdatedOnUtc — would leave it
        // sitting wherever it was while work visibly moved on.
        //
        // Hooked here rather than in the DbContext because reaching a root from a deep child (a checklist
        // item is five navigations from its request) means either loading those graphs or querying on
        // every save. The event already names the root, so the id is in hand and the touch is free.
        await _touch.TouchAsync(activityEvent.EntityType, activityEvent.EntityId, cancellationToken);
    }
}
