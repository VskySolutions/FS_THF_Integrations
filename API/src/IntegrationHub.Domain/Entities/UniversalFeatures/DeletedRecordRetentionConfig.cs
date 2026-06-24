namespace IntegrationHub.Domain.Entities;

/// <summary>
/// Per-tenant configuration of how long soft-deleted records are retained before they are flagged
/// as overdue for permanent deletion. One row per tenant.
/// </summary>
public class DeletedRecordRetentionConfig : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped); one config row per tenant.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Number of days a soft-deleted record is retained before it is overdue. Default 90.</summary>
    public int RetentionDays { get; set; } = 90;
}
