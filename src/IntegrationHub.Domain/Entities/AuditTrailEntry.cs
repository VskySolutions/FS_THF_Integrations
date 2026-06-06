namespace IntegrationHub.Domain.Entities;

/// <summary>
/// Immutable, append-only change record. Written by the Integration API and
/// Background Worker; never updated or deleted via application code.
/// </summary>
public class AuditTrailEntry
{
    /// <summary>Primary key.</summary>
    public long Id { get; set; }

    /// <summary>Name of the entity or domain concept the change applies to.</summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>Identifier of the affected entity instance.</summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>Action performed (e.g. "Created", "StatusChanged").</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Serialized details of the change (JSON or free text).</summary>
    public string? Details { get; set; }

    /// <summary>Identity of the actor that performed the change.</summary>
    public string? PerformedBy { get; set; }

    /// <summary>UTC timestamp when the change occurred (Audit &amp; Logging schema: CreatedDate).</summary>
    public DateTime CreatedDate { get; set; }
}
