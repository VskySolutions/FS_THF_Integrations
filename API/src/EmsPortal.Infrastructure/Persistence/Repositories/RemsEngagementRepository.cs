using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmsPortal.Infrastructure.Persistence.Repositories;

internal sealed class RemsEngagementRepository : IRemsEngagementRepository
{
    private readonly EmsPortalDbContext _dbContext;

    public RemsEngagementRepository(EmsPortalDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<REMSEngagement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.RemsEngagements
            .Include(e => e.MarketingMethods)
            .Include(e => e.CommissionSplits)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<REMSEngagement?> GetByEntityIdAsync(Guid remsEntityId, CancellationToken cancellationToken = default)
        => _dbContext.RemsEngagements
            .Include(e => e.MarketingMethods)
            .Include(e => e.CommissionSplits)
            .FirstOrDefaultAsync(e => e.REMSEntityId == remsEntityId, cancellationToken);

    public async Task AddAsync(REMSEngagement engagement, CancellationToken cancellationToken = default)
        => await _dbContext.RemsEngagements.AddAsync(engagement, cancellationToken);

    public void Update(REMSEngagement engagement) => _dbContext.RemsEngagements.Update(engagement);

    public void Remove(REMSEngagement engagement) => _dbContext.RemsEngagements.Remove(engagement);

    public Task<REMSEngagementAuditDetail?> GetAuditDetailAsync(Guid engagementId, CancellationToken cancellationToken = default)
        => _dbContext.RemsEngagementAuditDetails.FirstOrDefaultAsync(d => d.REMSEngagementId == engagementId, cancellationToken);

    public Task<REMSEngagementGovernmentDetail?> GetGovernmentDetailAsync(Guid engagementId, CancellationToken cancellationToken = default)
        => _dbContext.RemsEngagementGovernmentDetails.FirstOrDefaultAsync(d => d.REMSEngagementId == engagementId, cancellationToken);

    public Task<REMSEngagementTaxDetail?> GetTaxDetailAsync(Guid engagementId, CancellationToken cancellationToken = default)
        => _dbContext.RemsEngagementTaxDetails
            .Include(d => d.TaxForms)
            .FirstOrDefaultAsync(d => d.REMSEngagementId == engagementId, cancellationToken);

    public async Task AddAuditDetailAsync(REMSEngagementAuditDetail detail, CancellationToken cancellationToken = default)
        => await _dbContext.RemsEngagementAuditDetails.AddAsync(detail, cancellationToken);

    public async Task AddGovernmentDetailAsync(REMSEngagementGovernmentDetail detail, CancellationToken cancellationToken = default)
        => await _dbContext.RemsEngagementGovernmentDetails.AddAsync(detail, cancellationToken);

    public async Task AddTaxDetailAsync(REMSEngagementTaxDetail detail, CancellationToken cancellationToken = default)
        => await _dbContext.RemsEngagementTaxDetails.AddAsync(detail, cancellationToken);

    public async Task AddTaxFormAsync(REMSEngagementTaxForm taxForm, CancellationToken cancellationToken = default)
        => await _dbContext.RemsEngagementTaxForms.AddAsync(taxForm, cancellationToken);

    public async Task AddMarketingMethodAsync(REMSEngagementMarketingMethod method, CancellationToken cancellationToken = default)
        => await _dbContext.RemsEngagementMarketingMethods.AddAsync(method, cancellationToken);

    public void RemoveMarketingMethod(REMSEngagementMarketingMethod method) => _dbContext.RemsEngagementMarketingMethods.Remove(method);

    public async Task AddCommissionSplitAsync(REMSEngagementCommissionSplit split, CancellationToken cancellationToken = default)
        => await _dbContext.RemsEngagementCommissionSplits.AddAsync(split, cancellationToken);

    public void RemoveCommissionSplit(REMSEngagementCommissionSplit split) => _dbContext.RemsEngagementCommissionSplits.Remove(split);

    public Task<int> CountActiveCommissionSplitsAsync(Guid engagementId, CancellationToken cancellationToken = default)
        => _dbContext.RemsEngagementCommissionSplits.CountAsync(s => s.REMSEngagementId == engagementId, cancellationToken);
}
