using EmsPortal.Domain.Enums;

namespace EmsPortal.Domain.Entities;

/// <summary>
/// One round of approvals for a <see cref="REMSEngagement"/> (WO-110). Immutable history: a new round
/// (with a higher <see cref="RoundNumber"/> and a fresh set of tasks/checklists) is created on each
/// resubmission, and the row is written once more only on completion.
/// </summary>
public class REMSApprovalRound : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Owning engagement.</summary>
    public Guid REMSEngagementId { get; set; }

    /// <summary>Sequential round number within the engagement (1-based).</summary>
    public int RoundNumber { get; set; }

    /// <summary>Round outcome.</summary>
    public RemsApprovalRoundStatus Status { get; set; }

    /// <summary>When the round was sent for approval.</summary>
    public DateTime SentOnUtc { get; set; }

    /// <summary>User who sent the round.</summary>
    public Guid SentByUserId { get; set; }

    /// <summary>Reason captured when the round is rejected.</summary>
    public string? RejectionReason { get; set; }

    /// <summary>When the round completed (approved or rejected).</summary>
    public DateTime? CompletedOnUtc { get; set; }

    // ---- Navigations ----
    public REMSEngagement? Engagement { get; set; }
    public ICollection<REMSApprovalTask> Tasks { get; set; } = new List<REMSApprovalTask>();
}
