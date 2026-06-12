namespace IntegrationHub.Domain.Entities;

/// <summary>
/// DB-driven cron schedule for a recurring background job. Loaded by the
/// HangfireJobScheduler at startup so schedules can change without redeployment
/// (AC-INF-001.2).
/// </summary>
public class JobScheduleConfiguration : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Recurring job name (e.g. ExpenseImportJob). Unique per tenant.</summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>
    /// Owning tenant for a per-tenant import schedule. Null for platform-global schedules
    /// (the retry job and the legacy global import fan-out).
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>Cron expression controlling the job cadence.</summary>
    public string CronExpression { get; set; } = string.Empty;

    /// <summary>Whether this schedule is active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC timestamp when the schedule was last updated.</summary>
    public DateTime UpdatedDate { get; set; }
}
