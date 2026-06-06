using IntegrationHub.Domain.Entities;

namespace IntegrationHub.Application.Abstractions.Persistence;

/// <summary>
/// Append-only data access for <see cref="AuditTrailEntry"/> records. Exposes a
/// single write method — audit entries are immutable and are never updated or
/// deleted via application code (REQ-INF-009, AC-INF-009.3).
/// </summary>
public interface IAuditTrailRepository
{
    /// <summary>
    /// Stages an audit entry for insertion. The entry is committed within the same
    /// unit-of-work transaction as the action it records.
    /// </summary>
    Task AddAsync(AuditTrailEntry entry, CancellationToken cancellationToken = default);
}
