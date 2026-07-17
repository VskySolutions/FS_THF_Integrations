namespace EmsPortal.Domain.Entities;

/// <summary>
/// Links a <see cref="PermissionGroup"/> to a <see cref="Role"/> (composition junction). A role's
/// effective permission set is the union of the keys of all its active linked groups. Removed
/// automatically (cascade) when the group is deleted.
/// </summary>
public class RolePermissionGroup
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>The composed role.</summary>
    public Guid RoleId { get; set; }

    /// <summary>The permission group contributing to the role.</summary>
    public Guid PermissionGroupId { get; set; }

    public Role? Role { get; set; }
    public PermissionGroup? PermissionGroup { get; set; }
}
