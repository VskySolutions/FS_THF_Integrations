using EmsPortal.Domain.Enums;

namespace EmsPortal.Application.Abstractions.OptionSets;

/// <summary>
/// Translates between an option-set item's ID — which is what every table stores, as a foreign key — and
/// its CODE, which is what the application branches on.
///
/// <para>
/// Both directions are needed because the two are different kinds of thing. The ID is the reference: it is
/// per-tenant, because a tenant holds its own copy of each list, and it is what the database enforces. The
/// CODE is the contract: <c>"audit"</c> means the same thing in every tenant, which is why
/// <c>RemsEngagementCodes.DepartmentAudit</c> can be a constant and why the API keeps exposing codes to the
/// browser rather than ids. Comparing a stored ID to a constant would be wrong in every tenant but one.
/// </para>
/// <para>
/// Resolution is CACHED per (tenant, list). These lists change about never — the values the application
/// branches on are locked against deletion and re-coding (<c>OptionSetItem.IsSystem</c>) — so the cache
/// would be near-permanent, but it is evicted on every option-set write all the same: a tenant taking their
/// own copy of a standard list changes which ids are effective for them, and that must take effect at once.
/// </para>
/// </summary>
public interface IOptionCodeResolver
{
    /// <summary>
    /// The item id a code resolves to in the caller's tenant, or null when the list has no such value —
    /// which happens when a tenant added the code and then removed it, or when a caller passes a code the
    /// list never had.
    /// </summary>
    Task<Guid?> IdOfAsync(EntityType entityType, string setKey, string? code, CancellationToken cancellationToken = default);

    /// <summary>
    /// The code an item id stands for, or null when the id is unknown. Unscoped by tenant: an item id is
    /// unique platform-wide, and a row already holding one is holding it whatever tenant reads it.
    /// </summary>
    Task<string?> CodeOfAsync(Guid? itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The codes for several ids in one read — what every list and packet builder uses, so a page of
    /// twenty requests costs one lookup rather than twenty.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> CodesOfAsync(
        IEnumerable<Guid?> itemIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every (code → id) pair in a list for the caller's tenant. For a caller resolving a whole payload at
    /// once — an engagement update naming a department, a service line and an industry.
    /// </summary>
    Task<IReadOnlyDictionary<string, Guid>> IdsByCodeAsync(
        EntityType entityType, string setKey, CancellationToken cancellationToken = default);

    /// <summary>Drops the cached lists. Called by the option-set service whenever a list or value changes.</summary>
    void Invalidate();
}
