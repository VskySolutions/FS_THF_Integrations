using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Domain.Entities;

/// <summary>
/// Centralized record for an uploaded media file (profile pictures, logos, attachments,
/// documents, future CRM assets). Referenced by other entities via its <see cref="Id"/>;
/// a single file may be referenced by many entities (WO-61).
/// </summary>
public class Media : AuditableEntity
{
    public Guid Id { get; set; }

    // ---- File information ----
    public MediaType MediaType { get; set; } = MediaType.Image;
    public MediaCategory MediaCategory { get; set; } = MediaCategory.Profile;
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string? FileExtension { get; set; }
    public string? MimeType { get; set; }
    public long FileSize { get; set; }

    // ---- Storage information ----
    public string? StorageProvider { get; set; }
    public string? RelativePath { get; set; }
    public string? PublicUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? PreviewUrl { get; set; }

    // ---- Image / media metadata ----
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? Resolution { get; set; }
    /// <summary>Duration in seconds for video/audio.</summary>
    public int? Duration { get; set; }

    // ---- Status ----
    public bool IsPublic { get; set; }
    public bool IsProcessed { get; set; }
    public bool IsArchived { get; set; }
}
