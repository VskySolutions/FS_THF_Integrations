using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;

namespace IntegrationHub.Infrastructure.Persistence.Repositories;

/// <summary>
/// Append-only EF Core implementation. Stages the entry on the shared context so it
/// commits in the same transaction as the action it records; exposes no update or
/// delete path.
/// </summary>
internal sealed class AuditTrailRepository : IAuditTrailRepository
{
    private readonly IntegrationHubDbContext _dbContext;

    public AuditTrailRepository(IntegrationHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(AuditTrailEntry entry, CancellationToken cancellationToken = default)
        => await _dbContext.AuditTrail.AddAsync(entry, cancellationToken);
}
