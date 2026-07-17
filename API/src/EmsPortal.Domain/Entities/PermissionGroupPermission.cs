namespace EmsPortal.Domain.Entities;

/// <summary>
/// A single permission key held by a <see cref="PermissionGroup"/> (junction row). The set is
/// replaced wholesale when a group's permissions are edited.
/// </summary>
public class PermissionGroupPermission
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning group.</summary>
    public Guid PermissionGroupId { get; set; }

    /// <summary>A permission key from <see cref="EmsPortal.Shared.Security.Permissions"/>.</summary>
    public string PermissionKey { get; set; } = string.Empty;

    public PermissionGroup? PermissionGroup { get; set; }
}
