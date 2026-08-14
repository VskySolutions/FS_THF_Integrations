using EmsPortal.Domain.Enums;

namespace EmsPortal.Domain.Entities;

/// <summary>
/// The engagement being set up by a <see cref="REMS"/> request — exactly one per request. Holds the
/// servicing team, fee estimate, realization and billing schedule, and routes through approval.
/// <see cref="Department"/>, <see cref="ServiceLine"/>, <see cref="SubServiceLine"/>,
/// <see cref="SubIndustry"/> and <see cref="BillingPeriod"/> store option-set codes.
/// <para>
/// It hangs off the REQUEST, not off a <see cref="REMSEntity"/>. The initiator fills the engagement
/// setup before the client is ever contacted, so there is no entity to attach it to when it is created —
/// entities only exist once the client's intake comes back. A client who wants a second engagement gets
/// a second request, and a client group with several businesses produces one request per business (see
/// <see cref="REMSAdditionalEntity"/>), which is what keeps this one-to-one.
/// </para>
/// </summary>
public class REMSEngagement : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The request this engagement belongs to (one-to-one).</summary>
    public Guid REMSId { get; set; }

    /// <summary>Owning department (option-set code).</summary>
    public string? Department { get; set; }

    /// <summary>Service line (option-set code).</summary>
    public string? ServiceLine { get; set; }

    /// <summary>
    /// The service actually being sold, one level below <see cref="ServiceLine"/> (option-set
    /// <c>REMS.SubServiceLine</c> code). Classification only — nothing branches on it.
    /// </summary>
    public string? SubServiceLine { get; set; }

    /// <summary>
    /// The client's trade, one level below the request's industry group (option-set
    /// <c>REMS.SubIndustry</c> code). The GROUP lives on the form record because it decides what the client
    /// is asked and is frozen once the intake goes out; this is internal classification, so it belongs to
    /// the engagement and stays editable for as long as the setup does.
    /// </summary>
    public string? SubIndustry { get; set; }

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

    /// <summary>How often the client is billed (option-set <c>REMS.BillingPeriod</c> code).</summary>
    public string? BillingPeriod { get; set; }

    /// <summary>How many bills are raised over the engagement.</summary>
    public int? NumberOfBills { get; set; }

    /// <summary>Engagement approval lifecycle status.</summary>
    public RemsEngagementStatus Status { get; set; }

    // ---- Navigations ----
    // Note: the 0..1 detail relationships (audit/government/tax) and the engagement-per-request link are
    // modelled as one-to-many at the EF level (child holds the FK, no principal reference nav) so that
    // "one active per parent" can be enforced by a filtered unique index (WHERE [Deleted] = 0) rather
    // than EF's non-filtered convention 1:1 index, which would block soft-delete + re-create.
    public REMS? Rems { get; set; }
    public ICollection<REMSEngagementMarketingMethod> MarketingMethods { get; set; } = new List<REMSEngagementMarketingMethod>();
    public ICollection<REMSEngagementCommissionSplit> CommissionSplits { get; set; } = new List<REMSEngagementCommissionSplit>();
    public ICollection<REMSEngagementApprover> Approvers { get; set; } = new List<REMSEngagementApprover>();
    public ICollection<REMSApprovalRound> ApprovalRounds { get; set; } = new List<REMSApprovalRound>();
}
