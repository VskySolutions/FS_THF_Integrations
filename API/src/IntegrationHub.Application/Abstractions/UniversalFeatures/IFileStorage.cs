namespace IntegrationHub.Application.Abstractions.UniversalFeatures;

/// <summary>
/// Abstraction over the binary file store used by Universal Feature attachments. The default
/// implementation persists files under the host content root; the contract allows swapping in a
/// cloud/object store later without touching the controllers.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Persists <paramref name="content"/> under <paramref name="folder"/> and returns the relative
    /// stored path (the value persisted on <c>Attachment.StoredPath</c>).
    /// </summary>
    Task<string> SaveAsync(string folder, string originalFileName, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Opens the stored file for reading, or null when it no longer exists.</summary>
    Task<Stream?> OpenAsync(string storedPath, CancellationToken cancellationToken = default);

    /// <summary>Permanently deletes the stored file. No-op when already gone.</summary>
    Task DeleteAsync(string storedPath, CancellationToken cancellationToken = default);
}
