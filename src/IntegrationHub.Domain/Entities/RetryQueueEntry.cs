namespace IntegrationHub.Domain.Entities;

/// <summary>
/// Schedules a failed <see cref="IntegrationJob"/> for a future retry attempt.
/// Written and read exclusively by the Background Worker.
/// </summary>
public class RetryQueueEntry
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Foreign key to the job awaiting retry.</summary>
    public Guid JobId { get; set; }

    /// <summary>1-based attempt number this retry represents.</summary>
    public int AttemptNumber { get; set; }

    /// <summary>UTC timestamp at which the next attempt becomes eligible to run.</summary>
    public DateTime NextAttemptUtc { get; set; }

    /// <summary>Error message from the attempt that triggered this retry.</summary>
    public string? LastError { get; set; }

    /// <summary>UTC timestamp when this retry entry was created.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Navigation to the job awaiting retry.</summary>
    public IntegrationJob? Job { get; set; }
}
