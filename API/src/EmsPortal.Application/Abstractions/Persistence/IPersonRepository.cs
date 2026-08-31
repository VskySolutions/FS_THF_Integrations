using EmsPortal.Application.Common;
using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;

namespace EmsPortal.Application.Abstractions.Persistence;

/// <summary>Data access for the CRM <see cref="Person"/> master record (WO-61).</summary>
public interface IPersonRepository
{
    /// <summary>Loads a person with its primary address and profile media, scoped to the active tenant.</summary>
    Task<Person?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Loads a person ignoring the tenant filter — for cross-tenant Super Admin access.</summary>
    Task<Person?> GetByIdUnscopedAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Person?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> PersonCodeExistsAsync(string personCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Paginated list with optional free-text search (name, email, person code) and optional
    /// structured filters (owning tenant, whether the person is a user, active state, provenance) — all
    /// applied server-side so pagination/totals reflect the filtered set.
    /// <para>
    /// <c>sourceEntityType</c> narrows to persons of one provenance, e.g. <see cref="EntityType.Client"/>
    /// for the REMS client picker. Null lists every person whatever their source, including the rows that
    /// predate provenance tracking.
    /// </para>
    /// </summary>
    Task<(IReadOnlyList<Person> Items, int Total)> ListAsync(
        string? search, Guid? tenantId, bool? isUser, bool? isActive, SortRequest sort, int page, int limit,
        EntityType? sourceEntityType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// The client already holding this email address, if any. An email reaches one inbox, so two client
    /// records under one address are the same client entered twice — the second is refused rather than
    /// filed. Scoped to clients (<see cref="EntityType.Client"/>): a colleague and a client may share an
    /// address without either being a duplicate of the other.
    /// <para>
    /// <c>excludingPersonId</c> names a client to disregard — the one the calling request already minted,
    /// so re-saving it is not a clash with itself.
    /// </para>
    /// </summary>
    Task<Person?> FindClientByEmailAsync(
        string email, Guid? excludingPersonId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lightweight projection for the user-create Person dropdown (id, name, email, user-link flag).
    /// <para>
    /// Scoped to the caller's active tenant by the ambient filter. <paramref name="tenantId"/> names a
    /// DIFFERENT tenant instead — for the tenant-management screen, which creates accounts in a tenant its
    /// Super Admin is not currently inside. Callers must gate it; the repository only obeys.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<(Person Person, bool IsUser)>> ListSelectableAsync(
        Guid? tenantId = null, CancellationToken cancellationToken = default);

    Task AddAsync(Person person, CancellationToken cancellationToken = default);

    void Update(Person person);

    /// <summary>Soft-deletes the person (the DbContext converts the delete to a <c>Deleted</c> flag).</summary>
    void Remove(Person person);
}
