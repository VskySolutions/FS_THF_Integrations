namespace IntegrationHub.Api.Models.Mappings;

/// <summary>A single field-mapping rule (request shape).</summary>
public sealed class MappingFieldModel
{
    public string SourceField { get; set; } = string.Empty;
    public string DestinationField { get; set; } = string.Empty;
    public string? TransformationRule { get; set; }
}

/// <summary>Replace the whole field set for a (tenant, flow).</summary>
public sealed class SaveFlowMappingRequest
{
    public List<MappingFieldModel> Fields { get; set; } = new();
}

public sealed record MappingFieldResponse(string SourceField, string DestinationField, string? TransformationRule);

/// <summary>One row per flow on the mapping list: how many field rules it has.</summary>
public sealed record FlowMappingSummary(
    string InterfaceName,
    string FlowLabel,
    string SourceSystem,
    string DestinationSystem,
    int FieldCount,
    DateTime? UpdatedOnUtc);

public sealed record FlowMappingDetail(
    string InterfaceName,
    string FlowLabel,
    string SourceSystem,
    string DestinationSystem,
    IReadOnlyList<MappingFieldResponse> Fields);
