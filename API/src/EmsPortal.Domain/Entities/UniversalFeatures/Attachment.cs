using EmsPortal.Domain.Enums;

namespace EmsPortal.Domain.Entities;

/// <summary>
/// A file uploaded against any entity record via the shared <c>(EntityType, EntityId)</c> key.
/// The binary is stored by the file-storage abstraction; this row holds the metadata + storage path.
/// </summary>
public class Attachment : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The kind of entity this attachment belongs to.</summary>
    public EntityType EntityType { get; set; }

    /// <summary>The id of the entity this attachment belongs to.</summary>
    public Guid EntityId { get; set; }

    /// <summary>Original uploaded file name (for display + download).</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Relative path/key under the configured file storage root.</summary>
    public string StoredPath { get; set; } = string.Empty;

    /// <summary>MIME content type reported on upload.</summary>
    public string? MimeType { get; set; }

    /// <summary>File size in bytes.</summary>
    public long FileSize { get; set; }

    /// <summary>Lower-cased extension including the dot (e.g. <c>.pdf</c>).</summary>
    public string? FileExtension { get; set; }
}
