using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmsPortal.Infrastructure.Persistence.Repositories;

/// <summary>
/// Append-only EF Core implementation. Stages the entry on the shared context so it
/// commits in the same transaction as the action it records; exposes no update or
/// delete path.
/// </summary>
internal sealed class AuditTrailRepository : IAuditTrailRepository
{
    private readonly EmsPortalDbContext _dbContext;

    public AuditTrailRepository(EmsPortalDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(AuditTrailEntry entry, CancellationToken cancellationToken = default)
        => await _dbContext.AuditTrail.AddAsync(entry, cancellationToken);

    public async Task<IReadOnlyList<AuditTrailEntry>> ListByEntityAsync(string entityName, string entityId, int limit = 100, CancellationToken cancellationToken = default)
        => await _dbContext.AuditTrail
            .IgnoreQueryFilters()
            .Where(e => e.EntityName == entityName && e.EntityId == entityId && !e.Deleted)
            .OrderByDescending(e => e.CreatedDate)
            .Take(limit)
            .ToListAsync(cancellationToken);
}
