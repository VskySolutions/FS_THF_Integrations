using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EmsPortal.Infrastructure.Persistence.Repositories;

internal sealed class CustomerRequestRepository : ICustomerRequestRepository
{
    private readonly EmsPortalDbContext _dbContext;

    public CustomerRequestRepository(EmsPortalDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<CustomerRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.CustomerRequests
            .Include(c => c.Tenant)
            .Include(c => c.Address)
            .Include(c => c.Documents)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<CustomerRequest?> GetByIdForTenantAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
        => _dbContext.CustomerRequests
            .IgnoreQueryFilters()
            .Include(c => c.Tenant)
            .Include(c => c.Address)
            .Include(c => c.Documents)
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && !c.Deleted, cancellationToken);

    public Task<CustomerRequest?> GetByIdUnscopedAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.CustomerRequests
            .IgnoreQueryFilters()
            .Include(c => c.Tenant)
            .Include(c => c.Address)
            .Include(c => c.Documents)
            .FirstOrDefaultAsync(c => c.Id == id && !c.Deleted, cancellationToken);

    public async Task<(IReadOnlyList<CustomerRequest> Items, int Total)> ListAsync(
        string? search,
        Guid? tenantId,
        CustomerRequestStatus? status,
        Guid? submittedById,
        DateTime? fromUtc,
        DateTime? toUtc,
        Guid? draftViewerId,
        int page,
        int limit,
        CancellationToken cancellationToken = default,
        IReadOnlyCollection<Guid>? pinnedFirstIds = null)
    {
        // Cross-tenant (Super Admin) reads pass an explicit tenant id and bypass the ambient filter.
        var query = (tenantId is { } tid
            ? _dbContext.CustomerRequests.IgnoreQueryFilters().Where(c => c.TenantId == tid && !c.Deleted)
            : _dbContext.CustomerRequests.AsQueryable())
            .Include(c => c.Tenant)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c =>
                c.CompanyName.Contains(term) ||
                c.LegalName.Contains(term) ||
                c.EmailAddress.Contains(term) ||
                (c.CustomerRequestNumber != null && c.CustomerRequestNumber.Contains(term)));
        }

        if (status is { } st)
        {
            query = query.Where(c => c.Status == st);
        }
        if (submittedById is { } sub)
        {
            query = query.Where(c => c.SubmittedById == sub);
        }
        if (fromUtc is { } from)
        {
            query = query.Where(c => c.CreatedOnUtc >= from);
        }
        if (toUtc is { } to)
        {
            query = query.Where(c => c.CreatedOnUtc <= to);
        }
        // Draft records are private to their creator: hide other users' drafts.
        if (draftViewerId is { } viewer)
        {
            query = query.Where(c => c.Status != CustomerRequestStatus.Draft || c.CreatedById == viewer);
        }

        var total = await query.CountAsync(cancellationToken);

        // Float the caller's pinned records to the top so they land on the first page, then newest-first.
        var ordered = pinnedFirstIds is { Count: > 0 }
            ? query.OrderByDescending(c => pinnedFirstIds.Contains(c.Id)).ThenByDescending(c => c.UpdatedOnUtc)
            : query.OrderByDescending(c => c.UpdatedOnUtc);

        var items = await ordered
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<IReadOnlyList<CustomerRequest>> FindStep1DuplicatesAsync(
        Guid tenantId, Guid excludeId, string companyName, string legalName, string emailAddress, CancellationToken cancellationToken = default)
        => await _dbContext.CustomerRequests
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && !c.Deleted && c.Id != excludeId &&
                (c.CompanyName == companyName || c.LegalName == legalName || c.EmailAddress == emailAddress))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CustomerRequest>> FindTaxNumberDuplicatesAsync(
        Guid tenantId, Guid excludeId, string taxNumber, CancellationToken cancellationToken = default)
        => await _dbContext.CustomerRequests
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && !c.Deleted && c.Id != excludeId &&
                c.TaxNumber != null && c.TaxNumber == taxNumber)
            .ToListAsync(cancellationToken);

    // Numbers are assigned at creation, so the per-year sequence counts numbered requests by their
    // creation year (includes Drafts) — the basis for the next CUS-{year}-{seq} value.
    public Task<int> CountForYearAsync(Guid tenantId, int year, CancellationToken cancellationToken = default)
        => _dbContext.CustomerRequests
            .IgnoreQueryFilters()
            .CountAsync(c => c.TenantId == tenantId && c.CustomerRequestNumber != null && c.CreatedOnUtc.Year == year, cancellationToken);

    public async Task AddAsync(CustomerRequest request, CancellationToken cancellationToken = default)
        => await _dbContext.CustomerRequests.AddAsync(request, cancellationToken);

    public void Update(CustomerRequest request) => _dbContext.CustomerRequests.Update(request);

    public void Remove(CustomerRequest request) => _dbContext.CustomerRequests.Remove(request);
}
