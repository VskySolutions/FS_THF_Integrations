namespace IntegrationHub.Api.Models.UniversalFeatures;

/// <summary>Request to create a sticky note (personal, or tenant-broadcast when <c>IsPersonal=false</c>).</summary>
public sealed class CreateStickyNoteRequest
{
    public string? Title { get; set; }
    public string Body { get; set; } = string.Empty;
    public string Colour { get; set; } = string.Empty;

    /// <summary><c>global</c> or a specific route path.</summary>
    public string Scope { get; set; } = "global";

    public bool IsPersonal { get; set; } = true;

    /// <summary>Optional expiry for tenant notes (UTC).</summary>
    public DateTime? ExpiresAtUtc { get; set; }
}

/// <summary>Request to edit a sticky note.</summary>
public sealed class UpdateStickyNoteRequest
{
    public string? Title { get; set; }
    public string Body { get; set; } = string.Empty;
    public string Colour { get; set; } = string.Empty;
    public string Scope { get; set; } = "global";
    public DateTime? ExpiresAtUtc { get; set; }
}

/// <summary>Request to upsert a user's sticky-note layout state (position/size/z-order).</summary>
public sealed class StickyNoteStateRequest
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool IsMinimised { get; set; }
    public int ZIndex { get; set; }
}

/// <summary>A user's persisted sticky-note layout state.</summary>
public sealed record StickyNoteStateResponse(double X, double Y, double Width, double Height, bool IsMinimised, int ZIndex);

/// <summary>A sticky note as returned to the client, including the caller's layout state.</summary>
public sealed record StickyNoteResponse(
    Guid Id,
    string? Title,
    string Body,
    string Colour,
    string Scope,
    bool IsPersonal,
    bool IsOwner,
    DateTime? ExpiresAtUtc,
    DateTime CreatedOnUtc,
    StickyNoteStateResponse? State);

/// <summary>A tenant sticky note with its dismissal count, for the admin management list.</summary>
public sealed record AdminStickyNoteResponse(
    Guid Id, string? Title, string Body, string Colour, string Scope, DateTime? ExpiresAtUtc, int DismissalCount, DateTime CreatedOnUtc);
