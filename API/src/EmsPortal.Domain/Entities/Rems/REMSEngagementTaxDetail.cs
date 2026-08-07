namespace EmsPortal.Domain.Entities;

/// <summary>
/// Tax-specific detail for a <see cref="REMSEngagement"/> (WO-110), at most one per engagement.
/// <see cref="CalculatedDueDates"/> holds the derived due-date schedule as JSON; the applicable tax
/// forms are tracked via <see cref="TaxForms"/>.
/// </summary>
public class REMSEngagementTaxDetail : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Owning engagement.</summary>
    public Guid REMSEngagementId { get; set; }

    /// <summary>Fiscal year end (date-only).</summary>
    public DateOnly? FiscalYearEnd { get; set; }

    /// <summary>Calculated due dates as JSON.</summary>
    public string? CalculatedDueDates { get; set; }

    // ---- Navigations ----
    public REMSEngagement? Engagement { get; set; }
    public ICollection<REMSEngagementTaxForm> TaxForms { get; set; } = new List<REMSEngagementTaxForm>();
}
