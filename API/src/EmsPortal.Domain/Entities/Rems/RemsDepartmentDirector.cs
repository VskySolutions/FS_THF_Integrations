namespace EmsPortal.Domain.Entities;

/// <summary>
/// One department-to-director mapping row for a tenant's <see cref="RemsSettings"/> (WO-114). When staff
/// set an engagement's department, the matching director prefills
/// <see cref="REMSEngagement.DepartmentDirectorId"/> (staff may override). At most one row per
/// (tenant, <see cref="DepartmentId"/>). Inherits the standard audit/soft-delete fields.
/// </summary>
public class RemsDepartmentDirector : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Owning settings row.</summary>
    public Guid RemsSettingsId { get; set; }

    /// <summary>The department this mapping is for: a foreign key to the <c>REMS.Department</c> item.</summary>
    public Guid DepartmentId { get; set; }

    /// <summary>The director (User) mapped to the department.</summary>
    public Guid DirectorUserId { get; set; }

    // ---- Navigations ----
    public OptionSetItem? Department { get; set; }
    public RemsSettings? Settings { get; set; }
}
