namespace EmsPortal.Domain.Entities;

/// <summary>
/// A marketing method attributed to a <see cref="REMSEngagement"/> (WO-110).
/// <see cref="MarketingMethodId"/> is a foreign key to an <see cref="OptionSetItem"/> in the
/// <c>REMSMarketing_MarketingMethods.MarketingMethodId</c> option set.
/// </summary>
public class REMSEngagementMarketingMethod : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Owning engagement.</summary>
    public Guid REMSEngagementId { get; set; }

    /// <summary>The selected marketing method (option-set item).</summary>
    public Guid MarketingMethodId { get; set; }

    // ---- Navigations ----
    public REMSEngagement? Engagement { get; set; }
    public OptionSetItem? MarketingMethod { get; set; }
}
