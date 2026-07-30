namespace EmsPortal.Domain.Entities;

/// <summary>
/// A supporting file attached to a <see cref="REMS"/> request (WO-110). Links the request to a
/// stored <see cref="Media"/> blob.
/// </summary>
public class REMSFiles : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Owning REMS request.</summary>
    public Guid REMSId { get; set; }

    /// <summary>The stored media blob.</summary>
    public Guid MediaId { get; set; }

    // ---- Navigations ----
    public REMS? Rems { get; set; }
    public Media? Media { get; set; }
}
