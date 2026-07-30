namespace EmsPortal.Domain.Entities;

/// <summary>
/// Government-specific detail for a <see cref="REMSEngagement"/> (WO-110), at most one per engagement.
/// Contract and purchase-order dates are date-only. Copied from the submitted form payload on submit.
/// </summary>
public class REMSEngagementGovernmentDetail : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Owning engagement.</summary>
    public Guid REMSEngagementId { get; set; }

    /// <summary>Contract start date.</summary>
    public DateOnly? ContractStartDate { get; set; }

    /// <summary>Contract end date.</summary>
    public DateOnly? ContractEndDate { get; set; }

    /// <summary>Original contract term description.</summary>
    public string? OriginalTerm { get; set; }

    /// <summary>Renewal terms description.</summary>
    public string? RenewalTerms { get; set; }

    /// <summary>Purchase order start date.</summary>
    public DateOnly? PurchaseOrderStartDate { get; set; }

    /// <summary>Purchase order end date.</summary>
    public DateOnly? PurchaseOrderEndDate { get; set; }

    /// <summary>Contract number.</summary>
    public string? ContractNumber { get; set; }

    /// <summary>Whether the Florida 1% state fee applies.</summary>
    public bool? FloridaOnePercentStateFeeApplies { get; set; }

    // ---- Navigations ----
    public REMSEngagement? Engagement { get; set; }
}
