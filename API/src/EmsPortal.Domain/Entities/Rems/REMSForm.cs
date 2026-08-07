using EmsPortal.Domain.Enums;

namespace EmsPortal.Domain.Entities;

/// <summary>
/// The customer-facing onboarding form for a <see cref="REMS"/> request (WO-110). One active form per
/// request. <see cref="InviteCode"/> backs the public link and is immutable once the form is sent.
/// <see cref="IndustryGroup"/> stores the option-set <c>REMS.IndustryGroup</c> code.
/// </summary>
public class REMSForm : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Owning REMS request.</summary>
    public Guid REMSId { get; set; }

    /// <summary>Industry group driving the form's required fields (option-set <c>REMS.IndustryGroup</c> code).</summary>
    public string IndustryGroup { get; set; } = string.Empty;

    /// <summary>Public invite code, unique per tenant; immutable after send.</summary>
    public string InviteCode { get; set; } = string.Empty;

    /// <summary>Form lifecycle status.</summary>
    public RemsFormStatus Status { get; set; }

    /// <summary>Staff user who created the form.</summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>When the form was emailed to the customer.</summary>
    public DateTime? SentOnUtc { get; set; }

    /// <summary>When the customer submitted the form.</summary>
    public DateTime? SubmittedOnUtc { get; set; }

    /// <summary>When the invite code was locked (on send).</summary>
    public DateTime? InviteLockedOnUtc { get; set; }

    // ---- Navigations ----
    public REMS? Rems { get; set; }
    public ICollection<REMSFormDraft> Drafts { get; set; } = new List<REMSFormDraft>();
    public ICollection<REMSFormSubmission> Submissions { get; set; } = new List<REMSFormSubmission>();
    public ICollection<REMSFormEmailEvent> EmailEvents { get; set; } = new List<REMSFormEmailEvent>();
}
