using IntegrationHub.Application.Abstractions.UniversalFeatures;

namespace IntegrationHub.Api.Storage;

/// <summary>
/// Local-disk <see cref="IFileStorage"/>: persists attachment binaries under the host content root,
/// mirroring the existing media-upload storage. Stored paths are relative and forward-slashed so they
/// are portable across platforms.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly IWebHostEnvironment _environment;

    public LocalFileStorage(IWebHostEnvironment environment) => _environment = environment;

    public async Task<string> SaveAsync(string folder, string originalFileName, Stream content, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(originalFileName);
        var storedFileName = Guid.NewGuid().ToString("N") + extension;
        var relativeDir = folder.Replace('\\', '/').Trim('/');

        var absoluteDir = Path.Combine(_environment.ContentRootPath, relativeDir);
        Directory.CreateDirectory(absoluteDir);

        var absolutePath = Path.Combine(absoluteDir, storedFileName);
        await using (var stream = File.Create(absolutePath))
        {
            await content.CopyToAsync(stream, cancellationToken);
        }

        return $"{relativeDir}/{storedFileName}";
    }

    public Task<Stream?> OpenAsync(string storedPath, CancellationToken cancellationToken = default)
    {
        var absolutePath = ResolveAbsolute(storedPath);
        if (!File.Exists(absolutePath))
        {
            return Task.FromResult<Stream?>(null);
        }

        return Task.FromResult<Stream?>(File.OpenRead(absolutePath));
    }

    public Task DeleteAsync(string storedPath, CancellationToken cancellationToken = default)
    {
        var absolutePath = ResolveAbsolute(storedPath);
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }

        return Task.CompletedTask;
    }

    private string ResolveAbsolute(string storedPath)
        => Path.Combine(_environment.ContentRootPath, storedPath.Replace('/', Path.DirectorySeparatorChar));
}
