namespace EmsPortal.Domain.Entities;

/// <summary>
/// The latest in-progress draft of a <see cref="REMSForm"/> (WO-110). One draft per form; cascades
/// with its form. <see cref="DraftPayload"/> holds the REMSFormPayloadV1 JSON.
/// </summary>
public class REMSFormDraft : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Owning form.</summary>
    public Guid REMSFormId { get; set; }

    /// <summary>The draft answers as REMSFormPayloadV1 JSON.</summary>
    public string DraftPayload { get; set; } = string.Empty;

    /// <summary>When the draft was last saved.</summary>
    public DateTime LastSavedOnUtc { get; set; }

    // ---- Navigations ----
    public REMSForm? Form { get; set; }
}
