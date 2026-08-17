using System.Text;
using EmsPortal.Domain.Enums;

namespace EmsPortal.Application.Abstractions.Storage;

/// <summary>
/// The one place that knows the shape of the upload tree. Every module's files live at
/// <c>media-uploads/{tenantId:N}/{EntityType}/{recordKey}/{purpose}/{slug}__{shortId}{ext}</c>, e.g.
/// <c>media-uploads/8f3a…/Rems/REMS-42/client-acceptance/caf-signed-2026__4a2e88c1.pdf</c>.
/// <para>
/// <see cref="Root"/> stays the first segment of every stored path, which is what keeps the rows
/// written before this structure existed (<c>media-uploads/{guid}.png</c>) resolving unchanged: the
/// storage resolver joins the content root to whatever relative path the row carries, old or new.
/// </para>
/// </summary>
public static class StoragePaths
{
    /// <summary>First segment of every stored path, under the host content root.</summary>
    public const string Root = "media-uploads";

    /// <summary>Tenant segment used for files that belong to no single tenant.</summary>
    public const string SharedSegment = "shared";

    /// <summary>Holds uploads that have no parent record yet, date-sharded for a cleanup sweep.</summary>
    public const string UnassignedSegment = "_unassigned";

    /// <summary>Stands in for the date shard when an unparented upload arrived without a timestamp.</summary>
    private const string UndatedSegment = "undated";

    private const int MaxSlugLength = 60;
    private const int MaxSegmentLength = 80;
    private const string FallbackSlug = "file";

    /// <summary>The purpose folder names — the "what kind of file is this" segment of the tree.</summary>
    public static class Purposes
    {
        public const string Attachments = "attachments";
        public const string Certificates = "certificates";
        public const string ClientAcceptance = "client-acceptance";
        public const string Contracts = "contracts";
        public const string Documents = "documents";
        public const string Logos = "logos";
        public const string Other = "other";
        public const string Profile = "profile";
    }

    /// <summary>
    /// The purpose folder a media category files into. The category is what the uploader declares the
    /// file to be, so it — not the calling screen — decides the folder, and two screens uploading the
    /// same kind of document land in the same place.
    /// </summary>
    public static string PurposeFor(MediaCategory category) => category switch
    {
        MediaCategory.Attachment => Purposes.Attachments,
        MediaCategory.Certificate => Purposes.Certificates,
        MediaCategory.ClientAcceptance => Purposes.ClientAcceptance,
        MediaCategory.Contract => Purposes.Contracts,
        MediaCategory.Document => Purposes.Documents,
        MediaCategory.Logo => Purposes.Logos,
        MediaCategory.Profile => Purposes.Profile,
        _ => Purposes.Other,
    };

    /// <summary>Relative directory for a location — forward-slashed, no leading or trailing slash.</summary>
    public static string DirectoryFor(StorageLocation location)
    {
        var tenant = location.TenantId == Guid.Empty ? SharedSegment : location.TenantId.ToString("N");
        var purpose = Segment(location.Purpose, Purposes.Other);

        if (location.IsUnassigned)
        {
            // A parented location whose record key came back blank lands here too. It has no upload
            // timestamp to shard on, and dating it 1970 would read as a real (and very old) upload.
            var shard = location.UnassignedOnUtc is { } stamp ? $"{stamp:yyyy}/{stamp:MM}" : UndatedSegment;
            return $"{Root}/{tenant}/{UnassignedSegment}/{shard}/{purpose}";
        }

        return $"{Root}/{tenant}/{location.EntityType}/{Segment(location.RecordKey, "unknown")}/{purpose}";
    }

    /// <summary>
    /// The name the file is given on disk: the uploaded name slugged for readability plus a short
    /// unique suffix. The suffix is the head of the file's id, so a file on the server can be traced
    /// back to its row, and two uploads of the same name never collide.
    /// </summary>
    public static string FileNameFor(string? originalFileName, Guid fileId)
    {
        var extension = SafeExtension(Path.GetExtension(originalFileName ?? string.Empty));
        var slug = Slug(Path.GetFileNameWithoutExtension(originalFileName ?? string.Empty));
        return $"{slug}__{fileId.ToString("N")[..8]}{extension}";
    }

    /// <summary>The full relative stored path — what gets persisted on the row.</summary>
    public static string PathFor(StorageLocation location, string? originalFileName, Guid fileId)
        => $"{DirectoryFor(location)}/{FileNameFor(originalFileName, fileId)}";

    /// <summary>
    /// Lower-cased, dash-separated, ASCII-only form of an uploaded file name. Anything outside
    /// <c>[a-z0-9]</c> becomes a dash so nothing user-supplied can steer the path or upset a shell.
    /// </summary>
    private static string Slug(string value)
    {
        var builder = new StringBuilder(value.Length);
        var lastWasDash = false;
        foreach (var ch in value)
        {
            if (char.IsAsciiLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
                lastWasDash = false;
            }
            else if (!lastWasDash && builder.Length > 0)
            {
                builder.Append('-');
                lastWasDash = true;
            }

            if (builder.Length >= MaxSlugLength)
            {
                break;
            }
        }

        var slug = builder.ToString().Trim('-');
        return slug.Length == 0 ? FallbackSlug : slug;
    }

    /// <summary>
    /// A single path segment (record key, purpose) reduced to characters that are safe on every
    /// filesystem. Record keys arrive from the database rather than the request, but they still pass
    /// through here so no future caller can make a segment that escapes the tree.
    /// </summary>
    private static string Segment(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Trim())
        {
            if (char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_')
            {
                builder.Append(ch);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }

            if (builder.Length >= MaxSegmentLength)
            {
                break;
            }
        }

        var segment = builder.ToString().Trim('-', '_', '.');
        return segment.Length == 0 ? fallback : segment;
    }

    /// <summary>The extension kept verbatim only when it is a plain short alphanumeric one.</summary>
    private static string SafeExtension(string extension)
    {
        if (extension.Length is < 2 or > 11 || extension[0] != '.')
        {
            return string.Empty;
        }

        return extension[1..].All(char.IsAsciiLetterOrDigit) ? extension.ToLowerInvariant() : string.Empty;
    }
}
