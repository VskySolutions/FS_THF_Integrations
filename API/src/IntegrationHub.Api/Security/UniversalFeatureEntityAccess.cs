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
    /// <summary>
    /// The base read permission(s) gating UF access to a given entity type. Holding ANY one grants
    /// access. CustomerRequest accepts any customer-workflow capability (data entry / review / approve)
    /// so everyone who can open a customer record can read its notes, tags, activity, etc.
    /// </summary>
    public static IReadOnlyList<string> RequiredReadPermissions(EntityType entityType) => entityType switch
    {
        EntityType.CustomerRequest => new[] { Permissions.CustomersDataEntry, Permissions.CustomersReview, Permissions.CustomersApprove },
        EntityType.IntegrationJob => new[] { Permissions.JobsRead },
        EntityType.Tenant => new[] { Permissions.TenantsRead },
        EntityType.User => new[] { Permissions.UsersRead },
        EntityType.UserGroup => new[] { Permissions.UsersRead },
        _ => new[] { Permissions.UsersRead },
    };

    /// <summary>True when the caller may read the given entity type (and therefore its UF data).</summary>
    public static bool CanAccess(this ClaimsPrincipal principal, EntityType entityType)
        => principal.IsSuperAdmin() || RequiredReadPermissions(entityType).Any(principal.HasPermission);
}
