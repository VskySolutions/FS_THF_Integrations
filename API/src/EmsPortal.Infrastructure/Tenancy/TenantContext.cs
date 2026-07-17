using EmsPortal.Application.Abstractions.Tenancy;

namespace EmsPortal.Infrastructure.Tenancy;

/// <summary>Scoped, mutable tenant context. One instance per request or job scope.</summary>
internal sealed class TenantContext : ITenantContext
{
    public Guid TenantId { get; private set; } = Guid.Empty;

    public string TenantIdentifier { get; private set; } = string.Empty;

    public bool IsResolved { get; private set; }

    public void Set(Guid tenantId, string tenantIdentifier)
    {
        TenantId = tenantId;
        TenantIdentifier = tenantIdentifier ?? string.Empty;
        IsResolved = tenantId != Guid.Empty;
    }
}
