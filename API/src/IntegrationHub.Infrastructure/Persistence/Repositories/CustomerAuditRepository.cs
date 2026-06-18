using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence.Repositories;

internal sealed class CustomerAuditRepository : ICustomerAuditRepository
{
    private readonly IntegrationHubDbContext _dbContext;

    public CustomerAuditRepository(IntegrationHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(CustomerAuditEntry entry, CancellationToken cancellationToken = default)
        => await _dbContext.CustomerAuditEntries.AddAsync(entry, cancellationToken);

    public async Task<IReadOnlyList<CustomerAuditEntry>> ListByCustomerAsync(Guid customerRequestId, CancellationToken cancellationToken = default)
        => await _dbContext.CustomerAuditEntries
            .IgnoreQueryFilters()
            .Where(a => a.CustomerRequestId == customerRequestId && !a.Deleted)
            .OrderBy(a => a.PerformedOnUtc)
            .ToListAsync(cancellationToken);
}
