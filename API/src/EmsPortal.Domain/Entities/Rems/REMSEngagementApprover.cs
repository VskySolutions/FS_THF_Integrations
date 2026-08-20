namespace EmsPortal.Domain.Entities;

/// <summary>
/// A user picked to approve a <see cref="REMSEngagement"/> (WO-114, AC-REMS-018). Chosen on the workspace's
/// Approval tab, and editable there until the engagement is routed.
/// <para>
/// It holds only the approvers somebody CHOSE. The firm's shareholders, the CSE, the department director
/// and the commission recipients are on every round by standing — read from the role or the engagement
/// each time the list is built — and are never written here, which is what makes everything in this table
/// removable and everything routing by standing not.
/// </para>
/// <para>
/// Only the user is stored. The <see cref="Enums.RemsApproverRole"/> each approver acts under is derived
/// from their relationship to the engagement when the round is created, so a saved list cannot go stale if
/// the CSE or the commission recipients change after it was saved.
/// </para>
/// </summary>
public class REMSEngagementApprover : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The engagement this approver was picked for.</summary>
    public Guid REMSEngagementId { get; set; }

    /// <summary>The picked approver.</summary>
    public Guid UserId { get; set; }

    // ---- Navigations ----
    public REMSEngagement? Engagement { get; set; }
    public User? User { get; set; }
}
