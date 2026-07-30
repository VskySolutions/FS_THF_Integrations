namespace EmsPortal.Domain.Entities;

/// <summary>
/// Per-tenant REMS engagement/approval configuration (WO-114). Exactly one row per tenant holds the
/// firm-wide <see cref="ManagingShareholderUserId"/> (the managing shareholder who signs off on every
/// engagement) and the department-to-director mapping used to prefill an engagement's
/// <see cref="REMSEngagement.DepartmentDirectorId"/>. Both are optional ("unassigned placeholder"
/// allowed). Inherits the standard audit/soft-delete fields.
/// </summary>
public class RemsSettings : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped, one row per tenant).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The firm's managing shareholder (User); a required approver on every engagement when set.</summary>
    public Guid? ManagingShareholderUserId { get; set; }

    // ---- Navigations ----
    public ICollection<RemsDepartmentDirector> DepartmentDirectors { get; set; } = new List<RemsDepartmentDirector>();
}
