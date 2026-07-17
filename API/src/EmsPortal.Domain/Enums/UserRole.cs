namespace EmsPortal.Domain.Enums;

/// <summary>
/// Platform RBAC role. Names match the <c>EmsPortal.Shared.Security.Roles</c>
/// string constants used in JWT claims and authorization policies.
/// </summary>
public enum UserRole
{
    SuperAdmin = 0,
    TenantAdmin = 1,
    Operator = 2
}
