namespace IntegrationHub.Domain.Entities;

/// <summary>
/// A supporting document attached to a <see cref="CustomerRequest"/> (evidence for approvers).
/// Tenant-scoped and soft-deletable via the inherited <see cref="AuditableEntity"/> fields.
/// </summary>
public class CustomerDocument : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>The Customer Request this document is attached to.</summary>
    public Guid CustomerRequestId { get; set; }

    /// <summary>Owning tenant (matches the parent request).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Original uploaded file name.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Storage path/key where the file content is persisted.</summary>
    public string StoredPath { get; set; } = string.Empty;

    /// <summary>MIME content type of the file.</summary>
    public string? MimeType { get; set; }

    /// <summary>File size in bytes.</summary>
    public long FileSizeBytes { get; set; }

    /// <summary>User who uploaded the document.</summary>
    public Guid? UploadedById { get; set; }

    /// <summary>UTC timestamp of the upload.</summary>
    public DateTime UploadedOnUtc { get; set; }

    public CustomerRequest? CustomerRequest { get; set; }
}
