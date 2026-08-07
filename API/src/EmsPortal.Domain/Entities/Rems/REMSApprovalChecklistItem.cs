namespace EmsPortal.Domain.Entities;

/// <summary>
/// A single checklist line an approver completes on a <see cref="REMSApprovalTask"/> (WO-110). Ordered
/// by <see cref="DisplayOrder"/>, unique within the task.
/// </summary>
public class REMSApprovalChecklistItem : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Owning approval task.</summary>
    public Guid REMSApprovalTaskId { get; set; }

    /// <summary>Position within the task's checklist.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Checklist item text.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Whether the item has been completed.</summary>
    public bool IsCompleted { get; set; }

    /// <summary>When the item was completed.</summary>
    public DateTime? CompletedOnUtc { get; set; }

    // ---- Navigations ----
    public REMSApprovalTask? Task { get; set; }
}
