using System.Security.Claims;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Shared.Security;

namespace IntegrationHub.Api.Security;

/// <summary>
/// Maps a Universal Feature target <see cref="EntityType"/> to the base read permission of its parent
/// entity, and checks it on the current principal. Universal Feature operations (notes, tags, activity,
/// modified-log, …) require only the read permission of the record they attach to (Universal Features —
/// Authentication &amp; Security). Super Admins always pass.
/// </summary>
public static class UniversalFeatureEntityAccess
{
    /// <summary>The base read permission gating UF access to a given entity type.</summary>
    public static string RequiredReadPermission(EntityType entityType) => entityType switch
    {
        EntityType.CustomerRequest => Permissions.CustomersDataEntry,
        EntityType.IntegrationJob => Permissions.JobsRead,
        EntityType.Tenant => Permissions.TenantsRead,
        EntityType.User => Permissions.UsersRead,
        EntityType.UserGroup => Permissions.UsersRead,
        _ => Permissions.UsersRead,
    };

    /// <summary>True when the caller may read the given entity type (and therefore its UF data).</summary>
    public static bool CanAccess(this ClaimsPrincipal principal, EntityType entityType)
        => principal.IsSuperAdmin() || principal.HasPermission(RequiredReadPermission(entityType));
}
