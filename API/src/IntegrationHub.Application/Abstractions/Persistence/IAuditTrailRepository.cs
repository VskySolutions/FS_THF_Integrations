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

    /// <summary>
    /// Reads the audit entries for a single entity instance, newest first. Ignores the tenant
    /// filter so Super Admins can read any tenant's history (entity ids are globally unique).
    /// </summary>
    Task<IReadOnlyList<AuditTrailEntry>> ListByEntityAsync(string entityName, string entityId, int limit = 100, CancellationToken cancellationToken = default);
}
