using EmsPortal.Application.Abstractions.Auditing;
using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Application.Abstractions.Security;
using EmsPortal.Domain.Entities;

namespace EmsPortal.Infrastructure.Auditing;

/// <summary>
/// Append-only audit writer. Resolves the actor from <see cref="IActorAccessor"/> when
/// not supplied and stages the entry on the shared <see cref="IAuditTrailRepository"/>
/// so it commits in the same transaction as the recorded action.
/// </summary>
internal sealed class AuditTrailService : IAuditTrailService
{
    private readonly IAuditTrailRepository _repository;
    private readonly IActorAccessor _actorAccessor;

    public AuditTrailService(IAuditTrailRepository repository, IActorAccessor actorAccessor)
    {
        _repository = repository;
        _actorAccessor = actorAccessor;
    }

    public Task AddAsync(
        string entityName,
        string entityId,
        string action,
        string? details = null,
        string? performedBy = null,
        CancellationToken cancellationToken = default)
    {
        var entry = new AuditTrailEntry
        {
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            Details = details,
            PerformedBy = string.IsNullOrWhiteSpace(performedBy) ? _actorAccessor.GetCurrentActor() : performedBy,
            CreatedDate = DateTime.UtcNow,
        };

        return _repository.AddAsync(entry, cancellationToken);
    }
}
