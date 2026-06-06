namespace IntegrationHub.Domain.Enums;

/// <summary>
/// Status of a <see cref="Entities.RetryQueueEntry"/> in the retry lifecycle.
/// </summary>
public enum RetryStatus
{
    /// <summary>The entry is awaiting its next retry attempt.</summary>
    Pending = 0,

    /// <summary>The entry's job has exhausted all attempts and was dead-lettered.</summary>
    PermanentlyFailed = 1
}
