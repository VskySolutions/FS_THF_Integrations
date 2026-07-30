namespace EmsPortal.Domain.Entities;

/// <summary>
/// A commission share allocated to an employee on a <see cref="REMSEngagement"/> (WO-110). At most one
/// split per (engagement, employee); at most 10 active recipients per engagement (enforced at app
/// level). <see cref="CommissionPercentage"/> is greater than 0 and at most 100.
/// </summary>
public class REMSEngagementCommissionSplit : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Owning engagement.</summary>
    public Guid REMSEngagementId { get; set; }

    /// <summary>The employee receiving the split (User).</summary>
    public Guid EmployeeId { get; set; }

    /// <summary>Percentage share (&gt; 0 and &lt;= 100).</summary>
    public decimal CommissionPercentage { get; set; }

    // ---- Navigations ----
    public REMSEngagement? Engagement { get; set; }
}
