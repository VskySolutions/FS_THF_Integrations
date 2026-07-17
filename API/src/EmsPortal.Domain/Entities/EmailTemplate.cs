using EmsPortal.Domain.Enums;

namespace EmsPortal.Domain.Entities;

/// <summary>
/// A transactional email template. A row with a null <see cref="TenantId"/> is the platform-wide
/// default (managed by Super Admins); a row with a <see cref="TenantId"/> is that tenant's override
/// (managed by Tenant Admins). The effective template for a tenant is its override when present,
/// otherwise the global default. Inherits the standard audit/soft-delete fields.
/// </summary>
public class EmailTemplate : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant for an override, or null for the platform-wide default.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Which transactional email this template renders.</summary>
    public EmailTemplateKey TemplateKey { get; set; }

    /// <summary>Subject line; may contain <c>{{placeholders}}</c>.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>HTML body; may contain <c>{{placeholders}}</c>.</summary>
    public string Body { get; set; } = string.Empty;
}
