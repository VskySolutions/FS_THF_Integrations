namespace EmsPortal.Domain.Entities;

/// <summary>
/// The department a <see cref="User"/> belongs to within a tenant, and whether they head it. A user
/// holds at most one active department per tenant, and a department has at most one head — both enforced
/// by filtered unique indexes. The head IS the REMS department director: setting <see cref="IsHead"/>
/// upserts the matching <see cref="RemsDepartmentDirector"/> row, which is what prefills an engagement's
/// <see cref="REMSEngagement.DepartmentDirectorId"/>. Inherits the standard audit/soft-delete fields.
/// </summary>
public class UserDepartment : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped) — the same user may sit in a different department elsewhere.</summary>
    public Guid TenantId { get; set; }

    /// <summary>The user placed in the department.</summary>
    public Guid UserId { get; set; }

    /// <summary>Department code (option-set <c>REMS.Department</c> value, e.g. <c>audit</c>).</summary>
    public string Department { get; set; } = string.Empty;

    /// <summary>True when this user heads the department. At most one head per (tenant, department).</summary>
    public bool IsHead { get; set; }

    // ---- Navigations ----
    public User? User { get; set; }
}
