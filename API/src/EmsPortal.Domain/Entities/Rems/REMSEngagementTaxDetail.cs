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

    /// <summary>
    /// When the return is originally due. Derived from <see cref="FiscalYearEnd"/> — the 15th of the
    /// fourth month after it — and then EDITABLE: the rule covers the ordinary case, and the cases it
    /// does not cover were previously unrecordable because the schedule was computed and read-only.
    /// A column of its own now rather than only a line in <see cref="CalculatedDueDates"/>, since a value
    /// somebody typed has to survive a recalculation.
    /// </summary>
    public DateOnly? OriginalDueDate { get; set; }

    /// <summary>
    /// When the first extension expires. Derived as six months after <see cref="OriginalDueDate"/>, and
    /// editable for the same reason.
    /// </summary>
    public DateOnly? FirstExtensionDueDate { get; set; }

    /// <summary>
    /// The due-date schedule as JSON — now a SNAPSHOT of the two columns above rather than the only place
    /// they live. Kept because the approver's packet reads it, and because a round already sent for
    /// approval should keep the dates it was signed off against.
    /// </summary>
    public string? CalculatedDueDates { get; set; }

    // ---- Navigations ----
    public REMSEngagement? Engagement { get; set; }
    public ICollection<REMSEngagementTaxForm> TaxForms { get; set; } = new List<REMSEngagementTaxForm>();
}
