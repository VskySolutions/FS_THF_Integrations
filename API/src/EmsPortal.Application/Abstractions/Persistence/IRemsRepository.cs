using EmsPortal.Domain.Entities;

namespace EmsPortal.Application.Abstractions.Persistence;

/// <summary>
/// Data access for the REMS request aggregate root and its file links (WO-110). Tenant isolation is
/// applied by the DbContext global query filter; the number-generation helpers deliberately bypass it
/// to see every row for the tenant.
/// </summary>
public interface IRemsRepository
{
    /// <summary>The request with its file links loaded; tenant-scoped by the ambient filter.</summary>
    Task<REMS?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Active requests for the current tenant.</summary>
    Task<IReadOnlyList<REMS>> ListAsync(CancellationToken cancellationToken = default);

    Task AddAsync(REMS rems, CancellationToken cancellationToken = default);

    void Update(REMS rems);

    void Remove(REMS rems);

    Task AddFileAsync(REMSFiles file, CancellationToken cancellationToken = default);

    void RemoveFile(REMSFiles file);

    /// <summary>
    /// Count of active REMS requests for a tenant, ignoring the ambient query filter. Backs
    /// <c>REMS-{seq}</c> number generation (seq = count + 1).
    /// </summary>
    Task<int> CountActiveByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a REMS number is already taken (active) for the tenant. Advisory only — the filtered
    /// unique index <c>(TenantId, REMSNumber) WHERE [Deleted] = 0</c> is the definitive guard.
    /// </summary>
    Task<bool> NumberExistsAsync(Guid tenantId, string number, CancellationToken cancellationToken = default);
}
