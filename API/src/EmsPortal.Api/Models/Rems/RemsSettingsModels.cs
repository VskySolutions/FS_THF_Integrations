namespace EmsPortal.Api.Models.Rems;

// ---------------------------------------------------------------------------------------------------
// WO-114 — per-tenant REMS settings: the managing shareholder and the department-to-director mapping
// used to prefill an engagement's department director.
// ---------------------------------------------------------------------------------------------------

/// <summary>The tenant's REMS settings (WO-114): the managing shareholder and the department-director map.</summary>
public sealed record RemsSettingsView(
    RemsUserRef? ManagingShareholder,
    IReadOnlyList<RemsDepartmentDirectorView> DepartmentDirectors);

/// <summary>One department-to-director mapping row.</summary>
public sealed record RemsDepartmentDirectorView(string Department, RemsUserRef Director);

/// <summary>Update the tenant's REMS settings. The mapping list fully replaces the stored map.</summary>
public sealed class UpdateRemsSettingsRequest
{
    /// <summary>The managing shareholder (User id), or null to clear (unassigned placeholder).</summary>
    public Guid? ManagingShareholderUserId { get; set; }

    /// <summary>The full department-to-director map; reconciled against the stored rows (upsert + remove absent).</summary>
    public List<RemsDepartmentDirectorInput> DepartmentDirectors { get; set; } = new();
}

/// <summary>A single department-to-director mapping input.</summary>
public sealed class RemsDepartmentDirectorInput
{
    public string Department { get; set; } = string.Empty;
    public Guid DirectorUserId { get; set; }
}
