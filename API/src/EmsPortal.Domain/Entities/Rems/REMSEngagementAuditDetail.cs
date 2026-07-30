namespace EmsPortal.Domain.Entities;

/// <summary>
/// Audit-specific detail for a <see cref="REMSEngagement"/> (WO-110), at most one per engagement.
/// Holds the signed client-acceptance form required before an audit engagement can be approved.
/// </summary>
public class REMSEngagementAuditDetail : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Owning engagement.</summary>
    public Guid REMSEngagementId { get; set; }

    /// <summary>The uploaded client-acceptance form PDF (Media).</summary>
    public Guid? ClientAcceptanceFormMediaId { get; set; }

    // ---- Navigations ----
    public REMSEngagement? Engagement { get; set; }
    public Media? ClientAcceptanceFormMedia { get; set; }
}
