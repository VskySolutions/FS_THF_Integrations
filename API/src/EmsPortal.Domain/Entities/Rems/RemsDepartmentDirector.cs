namespace EmsPortal.Domain.Entities;

/// <summary>
/// One department-to-director mapping row for a tenant's <see cref="RemsSettings"/> (WO-114). When staff
/// set an engagement's <see cref="REMSEngagement.Department"/>, the matching director prefills
/// <see cref="REMSEngagement.DepartmentDirectorId"/> (staff may override). At most one row per
/// (tenant, <see cref="Department"/>). Inherits the standard audit/soft-delete fields.
/// </summary>
public class RemsDepartmentDirector : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Owning settings row.</summary>
    public Guid RemsSettingsId { get; set; }

    /// <summary>Department code (option-set <c>REMS.Department</c> value, e.g. <c>audit</c>).</summary>
    public string Department { get; set; } = string.Empty;

    /// <summary>The director (User) mapped to the department.</summary>
    public Guid DirectorUserId { get; set; }

    // ---- Navigations ----
    public RemsSettings? Settings { get; set; }
}
