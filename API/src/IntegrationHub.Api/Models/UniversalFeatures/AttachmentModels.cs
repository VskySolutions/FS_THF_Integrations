using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Api.Models.UniversalFeatures;

/// <summary>Attachment metadata as returned to the client.</summary>
public sealed record AttachmentResponse(
    Guid Id,
    EntityType EntityType,
    Guid EntityId,
    string FileName,
    string? MimeType,
    long FileSize,
    string? FileExtension,
    Guid? UploadedById,
    string? UploadedByName,
    DateTime CreatedOnUtc);
