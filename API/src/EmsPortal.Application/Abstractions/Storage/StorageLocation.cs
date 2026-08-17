using EmsPortal.Domain.Enums;

namespace EmsPortal.Application.Abstractions.Storage;

/// <summary>
/// Where an uploaded file belongs in the upload tree — the tenant that owns it, the record it hangs
/// off, and what kind of file it is. Handed to <see cref="IFileStorage"/> in place of a folder string
/// so that no caller ever composes a storage path by hand; <see cref="StoragePaths"/> is the only
/// thing that turns one of these into a path.
/// </summary>
/// <param name="TenantId">Owning tenant. <see cref="Guid.Empty"/> files under <c>shared</c>.</param>
/// <param name="EntityType">The kind of record the file hangs off; null when there is no parent.</param>
/// <param name="RecordKey">
/// The record's folder name — its human-readable number where it has one (<c>REMS-42</c>,
/// <c>PER-A1B2C3D4E5</c>), otherwise its id. Resolved by the API layer, never by the caller.
/// </param>
/// <param name="Purpose">The kind-of-file segment, e.g. <c>attachments</c> or <c>client-acceptance</c>.</param>
/// <param name="UnassignedOnUtc">Upload time, used to date-shard the <c>_unassigned</c> tree. Null when parented.</param>
public sealed record StorageLocation(
    Guid TenantId,
    EntityType? EntityType,
    string? RecordKey,
    string Purpose,
    DateTime? UnassignedOnUtc = null)
{
    /// <summary>True when the upload has no parent record and lands in the sweepable <c>_unassigned</c> tree.</summary>
    public bool IsUnassigned => EntityType is null || string.IsNullOrWhiteSpace(RecordKey);

    /// <summary>A file filed against a real record: <c>{tenant}/{entity}/{record}/{purpose}</c>.</summary>
    public static StorageLocation For(Guid tenantId, EntityType entityType, string recordKey, string purpose)
        => new(tenantId, entityType, recordKey, purpose);

    /// <summary>
    /// A file with nothing to hang off yet. Date-sharded so a cleanup sweep can age them out, and kept
    /// out of the entity tree so an orphan can never be mistaken for a record's file.
    /// </summary>
    public static StorageLocation Unassigned(Guid tenantId, string purpose, DateTime utcNow)
        => new(tenantId, null, null, purpose, utcNow);
}
