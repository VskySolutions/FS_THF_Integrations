using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Domain.Entities;

/// <summary>
/// Schedules a failed <see cref="IntegrationJob"/> for a future retry attempt.
/// Written and read exclusively by the Background Worker. Schema aligns with the
/// Error Handling &amp; Retry blueprint: Id, JobId, RetryCount, NextRetryDate, Status.
/// </summary>
public class RetryQueueEntry : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant; preserved so background retries run in the correct tenant scope.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Foreign key to the job awaiting retry.</summary>
    public Guid JobId { get; set; }

    /// <summary>Number of retry attempts made so far for the job.</summary>
    public int RetryCount { get; set; }

    /// <summary>UTC timestamp at which the next attempt becomes eligible to run.</summary>
    public DateTime NextRetryDate { get; set; }

    /// <summary>Lifecycle status of this retry entry.</summary>
    public RetryStatus Status { get; set; } = RetryStatus.Pending;

    /// <summary>Error message from the attempt that triggered this retry.</summary>
    public string? LastError { get; set; }

    /// <summary>UTC timestamp when this retry entry was created.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Navigation to the job awaiting retry.</summary>
    public IntegrationJob? Job { get; set; }
}
