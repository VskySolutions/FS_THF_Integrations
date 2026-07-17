using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmsPortal.Infrastructure.Persistence.Repositories;

internal sealed class CustomerAuditRepository : ICustomerAuditRepository
{
    private readonly EmsPortalDbContext _dbContext;

    public CustomerAuditRepository(EmsPortalDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(CustomerAuditEntry entry, CancellationToken cancellationToken = default)
        => await _dbContext.CustomerAuditEntries.AddAsync(entry, cancellationToken);

    public async Task<IReadOnlyList<CustomerAuditEntry>> ListByCustomerAsync(Guid customerRequestId, CancellationToken cancellationToken = default)
        => await _dbContext.CustomerAuditEntries
            .IgnoreQueryFilters()
            .Where(a => a.CustomerRequestId == customerRequestId && !a.Deleted)
            .OrderByDescending(a => a.PerformedOnUtc)
            .ToListAsync(cancellationToken);
}
