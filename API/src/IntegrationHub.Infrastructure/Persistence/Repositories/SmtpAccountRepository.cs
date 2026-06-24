using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core data access for <see cref="SmtpAccount"/>. Every query bypasses the ambient tenant query
/// filter and instead filters by the explicit tenant id, so a Super Admin managing another tenant via
/// the <c>?tenantId=</c> override reads/writes the correct tenant's accounts. Soft-deleted rows are
/// always excluded.
/// </summary>
internal sealed class SmtpAccountRepository : ISmtpAccountRepository
{
    private readonly IntegrationHubDbContext _dbContext;

    public SmtpAccountRepository(IntegrationHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<SmtpAccount>> ListByTenantAsync(Guid tenantId, bool? isActive, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Set<SmtpAccount>()
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId && !a.Deleted);

        if (isActive is { } active)
        {
            query = query.Where(a => a.IsActive == active);
        }

        return await query
            .OrderByDescending(a => a.IsActive)
            .ThenByDescending(a => a.CreatedOnUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<SmtpAccount?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
        => _dbContext.Set<SmtpAccount>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId && !a.Deleted, cancellationToken);

    public Task<SmtpAccount?> GetActiveAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _dbContext.Set<SmtpAccount>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.IsActive && !a.Deleted, cancellationToken);

    public Task<bool> NameExistsAsync(Guid tenantId, string accountName, Guid? excludeId, CancellationToken cancellationToken = default)
        => _dbContext.Set<SmtpAccount>()
            .IgnoreQueryFilters()
            .AnyAsync(a => a.TenantId == tenantId && !a.Deleted
                && a.AccountName == accountName
                && (excludeId == null || a.Id != excludeId), cancellationToken);

    public Task<int> CountByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _dbContext.Set<SmtpAccount>()
            .IgnoreQueryFilters()
            .CountAsync(a => a.TenantId == tenantId && !a.Deleted, cancellationToken);

    public async Task AddAsync(SmtpAccount account, CancellationToken cancellationToken = default)
        => await _dbContext.Set<SmtpAccount>().AddAsync(account, cancellationToken);

    public void Update(SmtpAccount account)
        => _dbContext.Set<SmtpAccount>().Update(account);

    public void Remove(SmtpAccount account)
        => _dbContext.Set<SmtpAccount>().Remove(account);
}
