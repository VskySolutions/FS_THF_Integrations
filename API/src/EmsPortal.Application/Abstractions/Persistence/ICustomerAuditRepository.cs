using EmsPortal.Domain.Entities;

namespace EmsPortal.Application.Abstractions.Persistence;

/// <summary>
/// Append-only data access for <see cref="CustomerAuditEntry"/> records.
/// </summary>
public interface ICustomerAuditRepository
{
    Task AddAsync(CustomerAuditEntry entry, CancellationToken cancellationToken = default);

    /// <summary>All audit entries for a request, chronological (oldest first).</summary>
    Task<IReadOnlyList<CustomerAuditEntry>> ListByCustomerAsync(Guid customerRequestId, CancellationToken cancellationToken = default);
}
