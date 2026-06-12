namespace IntegrationHub.Api.Models.Schedules;

/// <summary>A recurring job's (per-tenant) schedule for the admin Schedules screen.</summary>
public sealed record JobScheduleResponse(
    Guid? TenantId,
    string JobName,
    string DisplayName,
    string CronExpression,
    bool IsActive,
    bool Configured,
    DateTime? UpdatedOnUtc);

/// <summary>Update payload for a recurring job's schedule.</summary>
public sealed class UpdateJobScheduleRequest
{
    /// <summary>Standard 5-field cron expression (evaluated in UTC).</summary>
    public string CronExpression { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
