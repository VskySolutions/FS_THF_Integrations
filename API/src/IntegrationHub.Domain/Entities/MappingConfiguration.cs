using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Domain.Entities;

/// <summary>
/// Runtime field-mapping rules applied by transformers during a flow. Written by
/// the Admin API and read by the Background Worker on every transformer invocation.
/// </summary>
public class MappingConfiguration : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped mapping rules).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The flow/interface this field rule belongs to (e.g. "ExpenseImport"); scopes the rule to a job flow.</summary>
    public string InterfaceName { get; set; } = string.Empty;

    /// <summary>System the source field originates from.</summary>
    public SystemName SourceSystem { get; set; }

    /// <summary>System the destination field belongs to.</summary>
    public SystemName TargetSystem { get; set; }

    /// <summary>Source field name.</summary>
    public string SourceField { get; set; } = string.Empty;

    /// <summary>Destination field name the source value maps to.</summary>
    public string DestinationField { get; set; } = string.Empty;

    /// <summary>Optional transformation rule expression applied to the source value.</summary>
    public string? TransformationRule { get; set; }

    /// <summary>Optional serialized mapping metadata (legacy/auxiliary).</summary>
    public string? MappingJson { get; set; }

    /// <summary>Whether this configuration version is currently active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Monotonically increasing version number for this interface mapping.</summary>
    public int Version { get; set; } = 1;

    /// <summary>UTC timestamp when this configuration was created.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>UTC timestamp when this configuration was last updated.</summary>
    public DateTime? UpdatedAtUtc { get; set; }
}
