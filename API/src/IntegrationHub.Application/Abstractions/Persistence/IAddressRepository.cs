using IntegrationHub.Domain.Entities;

namespace IntegrationHub.Application.Abstractions.Persistence;

/// <summary>Data access for the reusable <see cref="Address"/> repository (WO-61).</summary>
public interface IAddressRepository
{
    Task<Address?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Address address, CancellationToken cancellationToken = default);

    void Update(Address address);
}
