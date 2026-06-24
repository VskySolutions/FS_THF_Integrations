using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Api.Models.UniversalFeatures;

/// <summary>A soft-deleted record in the Deleted Records Management list.</summary>
public sealed record DeletedRecordResponse(
    EntityType EntityType,
    Guid EntityId,
    string Identity,
    Guid TenantId,
    Guid? DeletedById,
    string? DeletedByName,
    DateTime? DeletedOnUtc,
    bool IsRetentionOverdue);

/// <summary>Request to restore a single soft-deleted record.</summary>
public sealed class RestoreRecordRequest
{
    public EntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
}

/// <summary>Request to restore multiple soft-deleted records of one entity type.</summary>
public sealed class BulkRestoreRequest
{
    public EntityType EntityType { get; set; }
    public List<Guid> EntityIds { get; set; } = new();
}

/// <summary>Request to permanently delete a soft-deleted record (token must match its identity).</summary>
public sealed class HardDeleteRequest
{
    public EntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public string ConfirmationToken { get; set; } = string.Empty;
}

/// <summary>Request to bulk-permanently-delete records (count must match for confirmation).</summary>
public sealed class BulkHardDeleteRequest
{
    public EntityType EntityType { get; set; }
    public List<Guid> EntityIds { get; set; } = new();
    public int ConfirmationCount { get; set; }
}

/// <summary>The per-record outcome of a bulk operation.</summary>
public sealed record BulkRecordResult(Guid EntityId, bool Success, string? Message);

/// <summary>The tenant's deleted-record retention period.</summary>
public sealed record RetentionConfigResponse(int RetentionDays);

/// <summary>Request to update the retention period.</summary>
public sealed class UpdateRetentionConfigRequest
{
    public int RetentionDays { get; set; }
}

/// <summary>A count of overdue records for one entity type.</summary>
public sealed record RetentionOverdueResponse(EntityType EntityType, int Count);
