namespace IntegrationHub.Domain.Enums;

/// <summary>
/// Lifecycle status of an <see cref="Entities.IntegrationJob"/>.
/// </summary>
public enum IntegrationJobStatus
{
    /// <summary>The job has been created and enqueued but has not started executing.</summary>
    Created = 0,

    /// <summary>The job is currently executing.</summary>
    Running = 1,

    /// <summary>The job completed successfully.</summary>
    Completed = 2,

    /// <summary>The job failed but remains eligible for retry.</summary>
    Failed = 3,

    /// <summary>The job exhausted its retries and moved to the dead-letter state.</summary>
    PermanentlyFailed = 4,

    /// <summary>Some records/lines succeeded while others failed and are pending retry.</summary>
    PartiallyFailed = 5
}
