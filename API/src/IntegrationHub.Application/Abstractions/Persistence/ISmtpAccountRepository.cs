using IntegrationHub.Domain.Entities;

namespace IntegrationHub.Application.Abstractions.Persistence;

/// <summary>
/// Data access for tenant SMTP accounts. Every method is scoped by an explicit
/// <c>tenantId</c> and bypasses the ambient query filter so a Super Admin can manage any
/// tenant's accounts via the <c>?tenantId=</c> override. Soft-deleted rows are always excluded.
/// </summary>
public interface ISmtpAccountRepository
{
    /// <summary>All non-deleted accounts for the tenant, optionally filtered by active state, newest first.</summary>
    Task<IReadOnlyList<SmtpAccount>> ListByTenantAsync(Guid tenantId, bool? isActive, CancellationToken cancellationToken = default);

    /// <summary>A single non-deleted account by id within the tenant, or null.</summary>
    Task<SmtpAccount?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>The tenant's active account, or null if none is active.</summary>
    Task<SmtpAccount?> GetActiveAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>True when another non-deleted account in the tenant uses the given name (case-insensitive), excluding <paramref name="excludeId"/>.</summary>
    Task<bool> NameExistsAsync(Guid tenantId, string accountName, Guid? excludeId, CancellationToken cancellationToken = default);

    /// <summary>Count of non-deleted accounts for the tenant.</summary>
    Task<int> CountByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task AddAsync(SmtpAccount account, CancellationToken cancellationToken = default);

    void Update(SmtpAccount account);

    void Remove(SmtpAccount account);
}
