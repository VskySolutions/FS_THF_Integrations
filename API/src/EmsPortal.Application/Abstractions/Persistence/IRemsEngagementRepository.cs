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

    /// <summary>
    /// The engagement with its full staff/approval context (WO-114): marketing methods, commission splits,
    /// and its entity → client (→ its other entities) → owning REMS request. Backs the approver-list build,
    /// the approval send/resubmit pre-checks, and the copy-from source/target lookups.
    /// </summary>
    Task<REMSEngagement?> GetWithContextAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The active engagement for an entity.</summary>
    Task<REMSEngagement?> GetByEntityIdAsync(Guid remsEntityId, CancellationToken cancellationToken = default);

    /// <summary>Active engagements for a set of entities (WO-114 workspace), with marketing and commission loaded.</summary>
    Task<IReadOnlyList<REMSEngagement>> ListByEntityIdsAsync(IReadOnlyCollection<Guid> entityIds, CancellationToken cancellationToken = default);

    /// <summary>Audit details for a set of engagements (WO-114 workspace).</summary>
    Task<IReadOnlyList<REMSEngagementAuditDetail>> ListAuditDetailsAsync(IReadOnlyCollection<Guid> engagementIds, CancellationToken cancellationToken = default);

    /// <summary>Government details for a set of engagements (WO-114 workspace).</summary>
    Task<IReadOnlyList<REMSEngagementGovernmentDetail>> ListGovernmentDetailsAsync(IReadOnlyCollection<Guid> engagementIds, CancellationToken cancellationToken = default);

    /// <summary>Tax details (with their tax forms) for a set of engagements (WO-114 workspace).</summary>
    Task<IReadOnlyList<REMSEngagementTaxDetail>> ListTaxDetailsAsync(IReadOnlyCollection<Guid> engagementIds, CancellationToken cancellationToken = default);

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

    void RemoveTaxForm(REMSEngagementTaxForm taxForm);

    Task AddMarketingMethodAsync(REMSEngagementMarketingMethod method, CancellationToken cancellationToken = default);

    void RemoveMarketingMethod(REMSEngagementMarketingMethod method);

    Task AddCommissionSplitAsync(REMSEngagementCommissionSplit split, CancellationToken cancellationToken = default);

    void RemoveCommissionSplit(REMSEngagementCommissionSplit split);

    /// <summary>Active commission-split count for an engagement (backs the max-10-recipients rule).</summary>
    Task<int> CountActiveCommissionSplitsAsync(Guid engagementId, CancellationToken cancellationToken = default);
}
