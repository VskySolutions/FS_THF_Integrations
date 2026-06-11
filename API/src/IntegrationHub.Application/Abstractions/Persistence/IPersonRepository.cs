using IntegrationHub.Domain.Entities;

namespace IntegrationHub.Application.Abstractions.Persistence;

/// <summary>Data access for the CRM <see cref="Person"/> master record (WO-61).</summary>
public interface IPersonRepository
{
    /// <summary>Loads a person with its primary address and profile media.</summary>
    Task<Person?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Person?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> PersonCodeExistsAsync(string personCode, CancellationToken cancellationToken = default);

    Task AddAsync(Person person, CancellationToken cancellationToken = default);

    void Update(Person person);
}
