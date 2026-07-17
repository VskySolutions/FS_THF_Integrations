namespace EmsPortal.Api.Models.Media;

public sealed record MediaResponse(
    Guid Id,
    string MediaType,
    string MediaCategory,
    string OriginalFileName,
    string? MimeType,
    long FileSize,
    string? PublicUrl,
    int? Width,
    int? Height);
