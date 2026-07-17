namespace EmsPortal.Domain.Entities;

/// <summary>
/// A floating sticky note. Personal notes (<see cref="IsPersonal"/> = true) belong to their creator;
/// tenant notes are broadcast to every user in the tenant until dismissed. The creator is the
/// inherited <see cref="AuditableEntity.CreatedById"/>.
/// </summary>
public class StickyNote : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The user who created the note (note owner).</summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>Optional title.</summary>
    public string? Title { get; set; }

    /// <summary>Note body.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Hex/named colour of the card.</summary>
    public string Colour { get; set; } = string.Empty;

    /// <summary>Visibility scope: <c>global</c> or a specific route path the note is pinned to.</summary>
    public string Scope { get; set; } = "global";

    /// <summary>True for a personal note; false for a tenant-broadcast note.</summary>
    public bool IsPersonal { get; set; } = true;

    /// <summary>Optional expiry (UTC) after which a tenant note is auto-removed.</summary>
    public DateTime? ExpiresAtUtc { get; set; }

    public ICollection<StickyNoteDismissal> Dismissals { get; set; } = new List<StickyNoteDismissal>();
    public ICollection<UserStickyNoteState> States { get; set; } = new List<UserStickyNoteState>();
}

/// <summary>Records that a user dismissed a tenant <see cref="StickyNote"/>.</summary>
public class StickyNoteDismissal : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The dismissed note.</summary>
    public Guid StickyNoteId { get; set; }

    /// <summary>The user who dismissed it.</summary>
    public Guid UserId { get; set; }

    public StickyNote? StickyNote { get; set; }
}

/// <summary>Per-user persisted position/size/z-order of a <see cref="StickyNote"/> (ADR-003).</summary>
public class UserStickyNoteState : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The note this state is for.</summary>
    public Guid StickyNoteId { get; set; }

    /// <summary>The user whose layout this is.</summary>
    public Guid UserId { get; set; }

    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool IsMinimised { get; set; }
    public int ZIndex { get; set; }

    public StickyNote? StickyNote { get; set; }
}
