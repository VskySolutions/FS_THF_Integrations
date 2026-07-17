using EmsPortal.Domain.Enums;

namespace EmsPortal.Api.Models.UniversalFeatures;

/// <summary>Request to pin an entity record.</summary>
public sealed class CreatePinRequest
{
    public EntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
}

/// <summary>A pinned record as returned to the client.</summary>
public sealed record PinResponse(Guid Id, EntityType EntityType, Guid EntityId, DateTime PinnedOnUtc);

/// <summary>Request to upsert (or clear) a colour assignment for an entity record.</summary>
public sealed class UpsertColourCodeRequest
{
    public EntityType EntityType { get; set; }
    public Guid EntityId { get; set; }

    /// <summary>Hex/named colour; <c>null</c> clears the assignment.</summary>
    public string? Colour { get; set; }
}

/// <summary>Request to export an entity record to PDF.</summary>
public sealed class PdfExportRequest
{
    public EntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public bool IncludeNotes { get; set; }
}
