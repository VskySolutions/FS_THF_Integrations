namespace EmsPortal.Domain.Entities;

/// <summary>
/// A tax form applicable to a <see cref="REMSEngagementTaxDetail"/> (WO-110). <see cref="TaxFormId"/>
/// is a foreign key to an <see cref="OptionSetItem"/> in the tax-form option set.
/// </summary>
public class REMSEngagementTaxForm : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Owning tax detail.</summary>
    public Guid REMSEngagementTaxDetailId { get; set; }

    /// <summary>The selected tax form (option-set item).</summary>
    public Guid TaxFormId { get; set; }

    // ---- Navigations ----
    public REMSEngagementTaxDetail? TaxDetail { get; set; }
    public OptionSetItem? TaxForm { get; set; }
}
