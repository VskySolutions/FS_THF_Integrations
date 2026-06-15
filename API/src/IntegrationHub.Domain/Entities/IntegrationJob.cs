using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Domain.Entities;

/// <summary>
/// Tracks the lifecycle of a single integration flow execution. Created by the
/// Integration API when a flow is triggered and updated by the Background Worker
/// as the job runs. Satisfies REQ-INF-002 (job status tracking).
/// </summary>
public class IntegrationJob : AuditableEntity
{
    /// <summary>Primary key. Returned to callers as the <c>jobId</c>.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant isolation, REQ-TNT-007).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Name of the integration interface/flow (e.g. "EmployeeSync").</summary>
    public string InterfaceName { get; set; } = string.Empty;

    /// <summary>Direction of data flow relative to the platform.</summary>
    public IntegrationDirection Direction { get; set; }

    /// <summary>System the data originates from.</summary>
    public SystemName SourceSystem { get; set; }

    /// <summary>System the data is delivered to.</summary>
    public SystemName TargetSystem { get; set; }

    /// <summary>Current lifecycle status.</summary>
    public IntegrationJobStatus Status { get; set; } = IntegrationJobStatus.Created;

    /// <summary>Correlation identifier propagated across logs for this job.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Number of execution attempts made so far.</summary>
    public int AttemptCount { get; set; }

    /// <summary>Most recent error message, if the job has failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>UTC timestamp when the job began executing.</summary>
    public DateTime? StartedAtUtc { get; set; }

    /// <summary>UTC timestamp when the job completed or permanently failed.</summary>
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>Log entries recorded for this job.</summary>
    public ICollection<IntegrationLog> Logs { get; set; } = new List<IntegrationLog>();
}
