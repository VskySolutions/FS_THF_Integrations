namespace EmsPortal.Domain.Entities;

/// <summary>
/// A legal entity belonging to a <see cref="REMSClient"/> (WO-110) — the main entity plus any related
/// subsidiaries captured on the form. Exactly one entity per client is flagged
/// <see cref="IsMainEntity"/>.
/// </summary>
public class REMSEntity : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Owning client.</summary>
    public Guid REMSClientId { get; set; }

    /// <summary>Entity/legal name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Employer Identification Number.</summary>
    public string? EIN { get; set; }

    /// <summary>True for the client's single main entity.</summary>
    public bool IsMainEntity { get; set; }

    // ---- Navigations ----
    public REMSClient? Client { get; set; }
    public ICollection<REMSEntityAddress> Addresses { get; set; } = new List<REMSEntityAddress>();
    public ICollection<REMSEntityContact> Contacts { get; set; } = new List<REMSEntityContact>();
}
