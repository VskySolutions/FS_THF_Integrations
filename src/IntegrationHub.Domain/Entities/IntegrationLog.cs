namespace IntegrationHub.Domain.Entities;

/// <summary>
/// Per-job request/response log entry written by the Background Worker on job
/// completion or failure. Satisfies AC-INF-002.3 (store request and response payloads).
/// </summary>
public class IntegrationLog
{
    /// <summary>Primary key.</summary>
    public long Id { get; set; }

    /// <summary>Foreign key to the owning <see cref="IntegrationJob"/>.</summary>
    public Guid JobId { get; set; }

    /// <summary>Severity level of the log entry (e.g. "Information", "Error").</summary>
    public string Level { get; set; } = "Information";

    /// <summary>Human-readable log message.</summary>
    public string? Message { get; set; }

    /// <summary>Serialized request payload sent to the external system.</summary>
    public string? RequestPayload { get; set; }

    /// <summary>Serialized response payload received from the external system.</summary>
    public string? ResponsePayload { get; set; }

    /// <summary>UTC timestamp when the entry was recorded.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Navigation to the owning job.</summary>
    public IntegrationJob? Job { get; set; }
}
