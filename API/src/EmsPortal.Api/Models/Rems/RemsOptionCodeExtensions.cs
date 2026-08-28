using EmsPortal.Application.Abstractions.OptionSets;
using EmsPortal.Domain.Enums;

namespace EmsPortal.Api.Models.Rems;

/// <summary>
/// Resolving a REMS option CODE to the item id a row stores, in the two shapes the feature writes them.
///
/// <para>
/// Every REMS option column is a foreign key to <c>OptionSetItem.Id</c>, but the codes are what the
/// application and the API contract are written in — a status transition says <c>"pending_approval"</c>,
/// not a guid. These two turn one into the other at the point of the write, against the CALLER's tenant.
/// </para>
/// </summary>
public static class RemsOptionCodeExtensions
{
    /// <summary>
    /// The item id for a REMS code, or null when the code is null/blank or the list has no such value.
    /// For the optional columns, where "not answered" is a legitimate state.
    /// </summary>
    public static Task<Guid?> RemsIdAsync(
        this IOptionCodeResolver codes, string setKey, string? code, CancellationToken cancellationToken = default)
        => codes.IdOfAsync(EntityType.Rems, setKey, code, cancellationToken);

    /// <summary>
    /// The item id for a REMS code the application itself sets — a status transition, the type a request is
    /// filed under. Throws when the list has no such value.
    ///
    /// <para>
    /// A throw rather than a null because there is no sensible way to carry on: these are the values the
    /// workflow is written in terms of, they are seeded, and they are locked against deletion and re-coding
    /// precisely so that this cannot happen. If it does, the tenant's list has been tampered with directly
    /// and the right answer is a 500 naming the missing value, not a row pointing at nothing.
    /// </para>
    /// </summary>
    public static async Task<Guid> RequireRemsIdAsync(
        this IOptionCodeResolver codes, string setKey, string code, CancellationToken cancellationToken = default)
        => await codes.IdOfAsync(EntityType.Rems, setKey, code, cancellationToken)
            ?? throw new InvalidOperationException(
                $"The '{setKey}' option list has no value '{code}'. It is a value the application writes, so "
                + "it should be present and locked — check the list in Administration → Option Sets.");
}
