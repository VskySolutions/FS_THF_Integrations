namespace EmsPortal.Domain.Entities;

/// <summary>
/// Standard audit and soft-delete fields applied to every persisted entity.
/// All timestamps are UTC. Populated automatically by the DbContext on save:
/// Created* on insert, Updated* on every change, and Deleted/DeletedOnUtc when a
/// delete is requested (rows are soft-deleted, never physically removed).
/// </summary>
public abstract class AuditableEntity
{
    /// <summary>Id of the user who created the row (null for system operations).</summary>
    public Guid? CreatedById { get; set; }

    /// <summary>UTC timestamp when the row was created.</summary>
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>Id of the user who last updated the row (null for system operations).</summary>
    public Guid? UpdatedById { get; set; }

    /// <summary>UTC timestamp when the row was last updated.</summary>
    public DateTime UpdatedOnUtc { get; set; }

    /// <summary>Soft-delete flag. Filtered out of all queries by default.</summary>
    public bool Deleted { get; set; }

    /// <summary>UTC timestamp when the row was soft-deleted; null while active.</summary>
    public DateTime? DeletedOnUtc { get; set; }
}
