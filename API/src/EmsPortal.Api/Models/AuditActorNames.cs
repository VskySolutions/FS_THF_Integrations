using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Domain.Entities;

namespace EmsPortal.Api.Models;

/// <summary>
/// Turns the actor an audit entry records into something a person can read.
/// <para>
/// <see cref="AuditTrailEntry.PerformedBy"/> holds the JWT subject — a user id — because that is the fact
/// that stays true: a name can change, an account can be renamed or deleted, and the row is immutable. So
/// every surface that shows an audit trail has to resolve it, or it shows a guid. This is that
/// resolution, in one place, so they all read the same.
/// </para>
/// <para>
/// Anything that is not a user id passes through as it stands: the <c>system</c> sentinel a background
/// write leaves (rendered as "System"), an API key's name for a machine caller, and the id of a user no
/// lookup can name any more.
/// </para>
/// </summary>
public static class AuditActorNames
{
    /// <summary>The actor recorded when nobody was signed in — see HttpContextActorAccessor.</summary>
    private const string SystemIdentity = "system";

    /// <summary>
    /// One lookup for a page of entries, returning the function that names each actor. Resolve the whole
    /// page at once rather than per row: an audit trail is a list, and a name per row is a query per row.
    /// </summary>
    public static async Task<Func<string?, string?>> ResolverAsync(
        IUserRepository users, IEnumerable<AuditTrailEntry> entries, CancellationToken cancellationToken)
    {
        var ids = entries
            .Select(e => e.PerformedBy)
            .Where(actor => Guid.TryParse(actor, out _))
            .Select(actor => Guid.Parse(actor!))
            .Distinct()
            .ToList();

        var names = ids.Count == 0
            ? new Dictionary<Guid, string>()
            : await users.GetFullNamesAsync(ids, cancellationToken);

        return actor =>
        {
            if (string.IsNullOrWhiteSpace(actor))
            {
                return null;
            }
            if (string.Equals(actor, SystemIdentity, StringComparison.OrdinalIgnoreCase))
            {
                return "System";
            }
            return Guid.TryParse(actor, out var id) && names.TryGetValue(id, out var name) ? name : actor;
        };
    }
}
