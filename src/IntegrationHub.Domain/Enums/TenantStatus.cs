namespace IntegrationHub.Domain.Enums;

/// <summary>
/// Lifecycle status of a <see cref="Entities.Tenant"/>.
/// </summary>
public enum TenantStatus
{
    /// <summary>Tenant is active; new jobs may be enqueued and executed.</summary>
    Active = 0,

    /// <summary>Tenant is deactivated; new job submissions are blocked, data preserved.</summary>
    Inactive = 1,

    /// <summary>Tenant is archived; retained read-only.</summary>
    Archived = 2
}
