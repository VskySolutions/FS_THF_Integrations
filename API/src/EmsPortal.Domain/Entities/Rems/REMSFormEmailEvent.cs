using EmsPortal.Domain.Enums;

namespace EmsPortal.Domain.Entities;

/// <summary>
/// An immutable, append-only delivery-tracking event for a <see cref="REMSForm"/> email (WO-110).
/// Never edited or soft-deleted. <see cref="ProviderPayload"/> holds the raw provider webhook JSON.
/// </summary>
public class REMSFormEmailEvent : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Owning form.</summary>
    public Guid REMSFormId { get; set; }

    /// <summary>Provider-assigned message id, when reported.</summary>
    public string? ProviderMessageId { get; set; }

    /// <summary>The kind of delivery event.</summary>
    public RemsFormEmailEventType EventType { get; set; }

    /// <summary>Recipient email address.</summary>
    public string RecipientEmail { get; set; } = string.Empty;

    /// <summary>When the event occurred.</summary>
    public DateTime OccurredOnUtc { get; set; }

    /// <summary>Raw provider webhook payload as JSON.</summary>
    public string? ProviderPayload { get; set; }

    // ---- Navigations ----
    public REMSForm? Form { get; set; }
}
