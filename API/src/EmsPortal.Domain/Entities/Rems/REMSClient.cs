namespace EmsPortal.Domain.Entities;

/// <summary>
/// The client record materialised from a completed <see cref="REMSFormSubmission"/> (WO-110). One
/// client per REMS request. <see cref="ReferralSource"/> stores the option-set code.
/// </summary>
public class REMSClient : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Owning REMS request.</summary>
    public Guid REMSId { get; set; }

    /// <summary>The immutable submission this client was created from.</summary>
    public Guid SourceFormSubmissionId { get; set; }

    /// <summary>Client display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Primary client email.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Client mobile number.</summary>
    public string? MobileNumber { get; set; }

    /// <summary>How the client was referred (option-set code).</summary>
    public string? ReferralSource { get; set; }

    /// <summary>The client's own follow-up detail for that referral source (who referred them, which event).</summary>
    public string? ReferralSourceDetail { get; set; }

    /// <summary>Billing contact name.</summary>
    public string? BillingContactName { get; set; }

    /// <summary>Billing email.</summary>
    public string? BillingEmail { get; set; }

    // Note: the billing ADDRESS is no longer here. It lives with the other two on the main entity, as a
    // REMSEntityAddress of type Billing — the client's physical, mailing and billing addresses share one
    // shape and one "at most one per (entity, type)" rule. Only the billing contact's name and email stay
    // on the client, because they describe a person rather than a place.

    // ---- Navigations ----
    public REMS? Rems { get; set; }
    public REMSFormSubmission? SourceFormSubmission { get; set; }
    public ICollection<REMSEntity> Entities { get; set; } = new List<REMSEntity>();
}
