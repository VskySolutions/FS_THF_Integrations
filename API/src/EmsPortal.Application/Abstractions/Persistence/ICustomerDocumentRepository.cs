using EmsPortal.Domain.Entities;

namespace EmsPortal.Application.Abstractions.Persistence;

/// <summary>
/// Data access for <see cref="CustomerDocument"/> attachments on a Customer Request.
/// </summary>
public interface ICustomerDocumentRepository
{
    Task<CustomerDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerDocument>> ListByCustomerAsync(Guid customerRequestId, CancellationToken cancellationToken = default);

    Task AddAsync(CustomerDocument document, CancellationToken cancellationToken = default);

    void Remove(CustomerDocument document);
}
