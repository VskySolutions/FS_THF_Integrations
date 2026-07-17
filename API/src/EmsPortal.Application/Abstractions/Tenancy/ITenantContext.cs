namespace EmsPortal.Application.Abstractions.Tenancy;

/// <summary>
/// Scoped service exposing the active tenant for the current request (API) or job
/// (Worker). Populated by TenantResolutionMiddleware from the JWT in the API, and from
/// the Hangfire job payload in the Worker. All repository queries and connector
/// credential lookups depend on this being resolved (Multi-Tenancy).
/// </summary>
public interface ITenantContext
{
    /// <summary>Active tenant id. <see cref="Guid.Empty"/> until resolved.</summary>
    Guid TenantId { get; }

    /// <summary>Active tenant identifier (slug). Empty until resolved.</summary>
    string TenantIdentifier { get; }

    /// <summary>True once a tenant has been resolved for this scope.</summary>
    bool IsResolved { get; }

    /// <summary>Sets the active tenant for the current scope.</summary>
    void Set(Guid tenantId, string tenantIdentifier);
}
