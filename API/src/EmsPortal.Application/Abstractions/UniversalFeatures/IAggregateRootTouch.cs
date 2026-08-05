using EmsPortal.Domain.Enums;

namespace EmsPortal.Application.Abstractions.UniversalFeatures;

/// <summary>
/// Marks an aggregate root as changed so the DbContext restamps its Updated By / Updated On, without the
/// caller needing the entity in hand.
/// <para>
/// The point is child writes. Editing an engagement, deciding an approval or sending a form touches only
/// that child row, yet the thing users track — and that the lists now order by — is the REQUEST. Bubbling
/// the timestamp keeps "last touched" meaning what people assume it means, and keeps a record that is
/// actively being worked at the top of the list rather than frozen at its last direct edit.
/// </para>
/// Implementations must be a no-op for entity types they do not own, and must never fail a save: a missed
/// timestamp is a cosmetic loss, whereas throwing here would roll back the real work that triggered it.
/// </summary>
public interface IAggregateRootTouch
{
    Task TouchAsync(EntityType entityType, Guid entityId, CancellationToken cancellationToken = default);
}
