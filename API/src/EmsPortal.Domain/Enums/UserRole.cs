namespace EmsPortal.Domain.Enums;

/// <summary>
/// Platform RBAC role tier stored on a tenant assignment. <see cref="SuperAdmin"/> and
/// <see cref="TenantAdmin"/> match the <c>EmsPortal.Shared.Security.Roles</c> string constants
/// used in JWT claims and authorization policies; <see cref="Custom"/> is the neutral sentinel for
/// an assignment governed by a custom, permission-based role rather than a fixed system tier.
/// </summary>
public enum UserRole
{
    SuperAdmin = 0,
    TenantAdmin = 1,
    Custom = 2
}
