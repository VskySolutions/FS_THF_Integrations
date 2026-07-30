using EmsPortal.Domain.Entities;

namespace EmsPortal.Application.Abstractions.Persistence;

/// <summary>
/// Data access for the REMS engagement aggregate (WO-110): the engagement, its audit/government/tax
/// detail records, tax forms, marketing methods and commission splits.
/// </summary>
public interface IRemsEngagementRepository
{
    /// <summary>The engagement with its marketing methods and commission splits loaded.</summary>
    Task<REMSEngagement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The active engagement for an entity.</summary>
    Task<REMSEngagement?> GetByEntityIdAsync(Guid remsEntityId, CancellationToken cancellationToken = default);

    Task AddAsync(REMSEngagement engagement, CancellationToken cancellationToken = default);

    void Update(REMSEngagement engagement);

    void Remove(REMSEngagement engagement);

    Task<REMSEngagementAuditDetail?> GetAuditDetailAsync(Guid engagementId, CancellationToken cancellationToken = default);

    Task<REMSEngagementGovernmentDetail?> GetGovernmentDetailAsync(Guid engagementId, CancellationToken cancellationToken = default);

    /// <summary>The tax detail for an engagement with its tax forms loaded.</summary>
    Task<REMSEngagementTaxDetail?> GetTaxDetailAsync(Guid engagementId, CancellationToken cancellationToken = default);

    Task AddAuditDetailAsync(REMSEngagementAuditDetail detail, CancellationToken cancellationToken = default);

    Task AddGovernmentDetailAsync(REMSEngagementGovernmentDetail detail, CancellationToken cancellationToken = default);

    Task AddTaxDetailAsync(REMSEngagementTaxDetail detail, CancellationToken cancellationToken = default);

    Task AddTaxFormAsync(REMSEngagementTaxForm taxForm, CancellationToken cancellationToken = default);

    Task AddMarketingMethodAsync(REMSEngagementMarketingMethod method, CancellationToken cancellationToken = default);

    void RemoveMarketingMethod(REMSEngagementMarketingMethod method);

    Task AddCommissionSplitAsync(REMSEngagementCommissionSplit split, CancellationToken cancellationToken = default);

    void RemoveCommissionSplit(REMSEngagementCommissionSplit split);

    /// <summary>Active commission-split count for an engagement (backs the max-10-recipients rule).</summary>
    Task<int> CountActiveCommissionSplitsAsync(Guid engagementId, CancellationToken cancellationToken = default);
}
