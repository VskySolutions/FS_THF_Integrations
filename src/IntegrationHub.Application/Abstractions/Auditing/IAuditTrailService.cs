namespace IntegrationHub.Application.Abstractions.Auditing;

/// <summary>
/// Writes append-only audit entries. The entry is staged on the shared unit of work
/// so it commits within the same transaction as the action it records — no action can
/// succeed without its audit entry (REQ-INF-009). Callers commit via
/// <see cref="Persistence.IUnitOfWork.SaveChangesAsync"/>.
/// </summary>
public interface IAuditTrailService
{
    /// <summary>
    /// Stages an audit entry. When <paramref name="performedBy"/> is null the actor is
    /// resolved from the current security context. <paramref name="details"/> captures
    /// optional structured context (e.g. a dead-letter failure reason and retry history).
    /// </summary>
    Task AddAsync(
        string entityName,
        string entityId,
        string action,
        string? details = null,
        string? performedBy = null,
        CancellationToken cancellationToken = default);
}
