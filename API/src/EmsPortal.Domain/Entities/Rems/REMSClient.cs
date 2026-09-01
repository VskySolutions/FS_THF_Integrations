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

    /// <summary>
    /// How the client was referred: a foreign key to the <c>REMS.ReferralSource</c> option-set item.
    ///
    /// <para>
    /// The ID rather than the code, so the database is what stops a referral source being deleted out from
    /// under the clients recorded against it. Nothing branches on this value -- it is classification -- but it
    /// follows the same rule as every other option-set reference on the platform.
    /// </para>
    /// </summary>
    public Guid? ReferralSourceId { get; set; }

    /// <summary>The client's own follow-up detail for that referral source (who referred them, which event).</summary>
    public string? ReferralSourceDetail { get; set; }

    /// <summary>
    /// Billing contact name. RETIRED: the intake form stopped asking for a billing contact once the
    /// addressee moved onto the billing address itself. Carried on submissions sent before that, and on
    /// whatever staff have typed here by hand since.
    /// </summary>
    public string? BillingContactName { get; set; }

    /// <summary>Billing email.</summary>
    public string? BillingEmail { get; set; }

    // Note: the billing ADDRESS is no longer here. Billing addresses live on the main entity, as
    // REMSEntityAddress rows of type Billing — every address the client gives shares one shape, and
    // there may be several billing ones. Only the two retired columns above stay on the client.

    // ---- Navigations ----
    public OptionSetItem? ReferralSource { get; set; }
    public REMS? Rems { get; set; }
    public REMSFormSubmission? SourceFormSubmission { get; set; }
    public ICollection<REMSEntity> Entities { get; set; } = new List<REMSEntity>();
}
