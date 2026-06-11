using IntegrationHub.Api.Models.Media;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DomainMedia = IntegrationHub.Domain.Entities.Media;
using MediaTypeEnum = IntegrationHub.Domain.Enums.MediaType;
using MediaCategoryEnum = IntegrationHub.Domain.Enums.MediaCategory;

namespace IntegrationHub.Api.Controllers;

/// <summary>
/// Centralized media upload/serving (WO-61). Files are stored under the content root and
/// streamed back by id; public media (e.g. profile pictures) can be fetched anonymously by
/// their unguessable id so they render in plain &lt;img&gt; tags.
/// </summary>
[ApiController]
[Produces("application/json")]
[Tags("Media")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
public sealed class MediaController : ControllerBase
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
    private const string StorageFolder = "media-uploads";

    private readonly IMediaRepository _media;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWebHostEnvironment _environment;

    public MediaController(IMediaRepository media, IUnitOfWork unitOfWork, IWebHostEnvironment environment)
    {
        _media = media;
        _unitOfWork = unitOfWork;
        _environment = environment;
    }

    [HttpPost("/api/media")]
    [Authorize]
    [RequestSizeLimit(MaxFileSizeBytes)]
    [ProducesResponseType<ApiResponse<MediaResponse>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile? file,
        [FromForm] string? mediaCategory,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed, "Validation failed.", "A non-empty file is required."));
        }
        if (file.Length > MaxFileSizeBytes)
        {
            return BadRequest(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed, "Validation failed.", "File exceeds the 10 MB limit."));
        }

        var category = Enum.TryParse<MediaCategoryEnum>(mediaCategory, ignoreCase: true, out var cat) ? cat : MediaCategoryEnum.Profile;
        var mediaType = ResolveMediaType(file.ContentType);
        var extension = Path.GetExtension(file.FileName);
        var id = Guid.NewGuid();
        var storedFileName = id.ToString("N") + extension;

        var storageRoot = Path.Combine(_environment.ContentRootPath, StorageFolder);
        Directory.CreateDirectory(storageRoot);
        var absolutePath = Path.Combine(storageRoot, storedFileName);
        await using (var stream = System.IO.File.Create(absolutePath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var media = new DomainMedia
        {
            Id = id,
            MediaType = mediaType,
            MediaCategory = category,
            OriginalFileName = file.FileName,
            StoredFileName = storedFileName,
            FileExtension = string.IsNullOrWhiteSpace(extension) ? null : extension.TrimStart('.'),
            MimeType = file.ContentType,
            FileSize = file.Length,
            StorageProvider = "Local",
            RelativePath = Path.Combine(StorageFolder, storedFileName).Replace('\\', '/'),
            PublicUrl = $"/api/media/{id}/content",
            // Profile pictures must be viewable in <img> tags without an auth header.
            IsPublic = category == MediaCategoryEnum.Profile,
            IsProcessed = true,
        };
        await _media.AddAsync(media, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponseFactory.Success(
            new MediaResponse(media.Id, media.MediaType.ToString(), media.MediaCategory.ToString(),
                media.OriginalFileName, media.MimeType, media.FileSize, media.PublicUrl, media.Width, media.Height),
            "Media uploaded."));
    }

    [HttpGet("/api/media/{id:guid}/content")]
    [AllowAnonymous]
    public async Task<IActionResult> Content(Guid id, CancellationToken cancellationToken)
    {
        var media = await _media.GetByIdAsync(id, cancellationToken);
        if (media is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Media not found."));
        }

        // Non-public media requires an authenticated caller.
        if (!media.IsPublic && User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("Authentication required."));
        }

        var absolutePath = Path.Combine(_environment.ContentRootPath, media.RelativePath ?? string.Empty);
        if (string.IsNullOrWhiteSpace(media.RelativePath) || !System.IO.File.Exists(absolutePath))
        {
            return NotFound(ApiResponseFactory.NotFound("Media file not found."));
        }

        var stream = System.IO.File.OpenRead(absolutePath);
        return File(stream, string.IsNullOrWhiteSpace(media.MimeType) ? "application/octet-stream" : media.MimeType);
    }

    private static MediaTypeEnum ResolveMediaType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return MediaTypeEnum.Document;
        }
        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return MediaTypeEnum.Image;
        if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)) return MediaTypeEnum.Video;
        if (contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)) return MediaTypeEnum.Audio;
        return MediaTypeEnum.Document;
    }
}
