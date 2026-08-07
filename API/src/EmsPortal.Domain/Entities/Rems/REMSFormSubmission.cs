namespace EmsPortal.Domain.Entities;

/// <summary>
/// An immutable, append-only snapshot of a completed <see cref="REMSForm"/> submission (WO-110). Never
/// edited or soft-deleted; the resulting <see cref="REMSClient"/> references the submission it was
/// created from. <see cref="SubmittedPayload"/> holds the REMSFormPayloadV1 JSON as submitted.
/// </summary>
public class REMSFormSubmission : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Owning form.</summary>
    public Guid REMSFormId { get; set; }

    /// <summary>The submitted answers as REMSFormPayloadV1 JSON (immutable).</summary>
    public string SubmittedPayload { get; set; } = string.Empty;

    /// <summary>When the customer submitted.</summary>
    public DateTime SubmittedOnUtc { get; set; }

    // ---- Navigations ----
    public REMSForm? Form { get; set; }
}
