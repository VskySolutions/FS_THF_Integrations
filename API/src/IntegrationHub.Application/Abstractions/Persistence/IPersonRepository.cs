using IntegrationHub.Domain.Entities;

namespace IntegrationHub.Application.Abstractions.Persistence;

/// <summary>Data access for the CRM <see cref="Person"/> master record (WO-61).</summary>
public interface IPersonRepository
{
    /// <summary>Loads a person with its primary address and profile media.</summary>
    Task<Person?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Person?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> PersonCodeExistsAsync(string personCode, CancellationToken cancellationToken = default);

    /// <summary>Paginated list with optional free-text search over name, email and person code.</summary>
    Task<(IReadOnlyList<Person> Items, int Total)> ListAsync(
        string? search, int page, int limit, CancellationToken cancellationToken = default);

    /// <summary>Lightweight projection for the user-create Person dropdown (id, name, email, user-link flag).</summary>
    Task<IReadOnlyList<(Person Person, bool IsUser)>> ListSelectableAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Person person, CancellationToken cancellationToken = default);

    void Update(Person person);

    /// <summary>Soft-deletes the person (the DbContext converts the delete to a <c>Deleted</c> flag).</summary>
    void Remove(Person person);
}
