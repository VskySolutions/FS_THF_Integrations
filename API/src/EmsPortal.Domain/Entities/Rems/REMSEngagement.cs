using EmsPortal.Domain.Enums;

namespace EmsPortal.Domain.Entities;

/// <summary>
/// An engagement created for a <see cref="REMSEntity"/> (WO-110), one per entity. Holds the servicing
/// team, fee estimate and realization, and routes through approval. <see cref="Department"/> and
/// <see cref="ServiceLine"/> store option-set codes.
/// </summary>
public class REMSEngagement : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The entity this engagement serves (one-to-one).</summary>
    public Guid REMSEntityId { get; set; }

    /// <summary>Owning department (option-set code).</summary>
    public string? Department { get; set; }

    /// <summary>Service line (option-set code).</summary>
    public string? ServiceLine { get; set; }

    /// <summary>Department director (User).</summary>
    public Guid? DepartmentDirectorId { get; set; }

    /// <summary>Engagement executive (User).</summary>
    public Guid? EngagementExecutiveId { get; set; }

    /// <summary>Billing manager (User).</summary>
    public Guid? BillingManagerId { get; set; }

    /// <summary>Estimated first-year fee.</summary>
    public decimal? FirstYearFeeEstimate { get; set; }

    /// <summary>Expected realization percentage (0–100).</summary>
    public decimal? RealizationPercentage { get; set; }

    /// <summary>Engagement approval lifecycle status.</summary>
    public RemsEngagementStatus Status { get; set; }

    // ---- Navigations ----
    // Note: the 0..1 detail relationships (audit/government/tax) and the engagement-per-entity link are
    // modelled as one-to-many at the EF level (child holds the FK, no principal reference nav) so that
    // "one active per parent" can be enforced by a filtered unique index (WHERE [Deleted] = 0) rather
    // than EF's non-filtered convention 1:1 index, which would block soft-delete + re-create.
    public REMSEntity? Entity { get; set; }
    public ICollection<REMSEngagementMarketingMethod> MarketingMethods { get; set; } = new List<REMSEngagementMarketingMethod>();
    public ICollection<REMSEngagementCommissionSplit> CommissionSplits { get; set; } = new List<REMSEngagementCommissionSplit>();
    public ICollection<REMSApprovalRound> ApprovalRounds { get; set; } = new List<REMSApprovalRound>();
}
