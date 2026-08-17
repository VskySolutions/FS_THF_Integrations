namespace EmsPortal.Application.Abstractions.Storage;

/// <summary>
/// The platform's binary file store — every module's uploads go through this one abstraction, so the
/// tree stays a single shape and swapping local disk for an object store later touches nothing else.
/// The default implementation persists files under the host content root.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Persists <paramref name="content"/> at the canonical path for <paramref name="location"/>.
    /// The caller supplies the location and the uploaded name; the path itself is not theirs to choose.
    /// </summary>
    Task<StoredFile> SaveAsync(StorageLocation location, string? originalFileName, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Opens a stored file for reading, or null when it no longer exists.</summary>
    Task<Stream?> OpenAsync(string storedPath, CancellationToken cancellationToken = default);

    /// <summary>Permanently deletes a stored file. No-op when already gone.</summary>
    Task DeleteAsync(string storedPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// A file that has landed in the store. <see cref="FileId"/> is minted by the store and is what the
/// owning row should use as its id — that is what makes the short suffix on <see cref="StoredFileName"/>
/// traceable back to the record.
/// </summary>
public sealed record StoredFile(Guid FileId, string RelativePath, string StoredFileName);
