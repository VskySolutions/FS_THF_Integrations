using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Domain.Entities;

namespace EmsPortal.Api.Models;

/// <summary>
/// The provenance block every detail page ends with: who made the record and when, who last touched it and
/// when, and — once it is deleted — who deleted it and when.
/// <para>
/// It travels as one nested object rather than as five more fields on each detail response. The facts are
/// the same on every screen, so they are one parameter per DTO and one prop on the component that renders
/// them, and no page can end up carrying "created" without "updated" because somebody added them one at a
/// time. The lists say the same four things through <c>useAuditColumns</c>, in the same order.
/// </para>
/// <para>
/// The deletion pair is null while the record is live, and is not backed by a column of its own: there is
/// no DeletedById anywhere in the schema, because a soft delete is a write like any other and leaves the
/// deleter in <see cref="AuditableEntity.UpdatedById"/>. The Deleted Records list has always read it that
/// way (see DeletedRecordsRepository), and this agrees with it by reading the same column.
/// </para>
/// </summary>
public sealed record RecordAudit(
    string? CreatedBy,
    DateTime CreatedOnUtc,
    string? UpdatedBy,
    DateTime UpdatedOnUtc,
    bool Deleted,
    string? DeletedBy,
    DateTime? DeletedOnUtc)
{
    /// <summary>
    /// The block for one record, from a name lookup the caller has already made. Use this wherever the
    /// response resolves other actors too, so the whole screen costs one query rather than one per block.
    /// </summary>
    public static RecordAudit From(AuditableEntity entity, Func<Guid?, string?> nameOf) => new(
        Actor(entity.CreatedById, nameOf),
        entity.CreatedOnUtc,
        Actor(entity.UpdatedById, nameOf),
        entity.UpdatedOnUtc,
        entity.Deleted,
        entity.Deleted ? Actor(entity.UpdatedById, nameOf) : null,
        entity.Deleted ? entity.DeletedOnUtc : null);

    /// <summary>
    /// The actor as a person reads it. A null id is not a missing name — it is the platform writing for
    /// itself, a seeder or a background job — so it reads "System", the same word the audit trail uses
    /// for its own <c>system</c> sentinel. An id that no longer names anybody (a purged account) stays
    /// null and the page says "Unknown": somebody did this, and we can no longer say who.
    /// </summary>
    private static string? Actor(Guid? id, Func<Guid?, string?> nameOf)
        => id is null ? "System" : nameOf(id);

    /// <summary>The same block, doing its own lookup — for a response with no other actor to resolve.</summary>
    public static async Task<RecordAudit> ForAsync(
        IUserRepository users, AuditableEntity entity, CancellationToken cancellationToken)
    {
        var ids = new[] { entity.CreatedById, entity.UpdatedById }
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var names = ids.Count == 0
            ? new Dictionary<Guid, string>()
            : await users.GetFullNamesAsync(ids, cancellationToken);

        return From(entity, Names(names));
    }

    /// <summary>Turns a name lookup into the <c>nameOf</c> the two above take.</summary>
    public static Func<Guid?, string?> Names(IReadOnlyDictionary<Guid, string> names)
        => id => id is { } userId && names.TryGetValue(userId, out var name) ? name : null;
}
