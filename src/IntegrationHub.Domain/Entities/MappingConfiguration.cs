using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Domain.Entities;

/// <summary>
/// Runtime field-mapping rules applied by transformers during a flow. Written by
/// the Admin API and read by the Background Worker on every transformer invocation.
/// </summary>
public class MappingConfiguration
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Interface/flow this mapping applies to.</summary>
    public string InterfaceName { get; set; } = string.Empty;

    /// <summary>System the source fields originate from.</summary>
    public SystemName SourceSystem { get; set; }

    /// <summary>System the target fields belong to.</summary>
    public SystemName TargetSystem { get; set; }

    /// <summary>Serialized mapping rule set (JSON).</summary>
    public string MappingJson { get; set; } = string.Empty;

    /// <summary>Whether this configuration version is currently active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Monotonically increasing version number for this interface mapping.</summary>
    public int Version { get; set; } = 1;

    /// <summary>UTC timestamp when this configuration was created.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>UTC timestamp when this configuration was last updated.</summary>
    public DateTime? UpdatedAtUtc { get; set; }
}
