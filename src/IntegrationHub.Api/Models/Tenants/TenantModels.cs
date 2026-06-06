namespace IntegrationHub.Api.Models.Tenants;

public sealed class CreateTenantRequest
{
    public string Name { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
}

public sealed class UpdateTenantRequest
{
    public string Name { get; set; } = string.Empty;
}

public sealed class UpdateTenantStatusRequest
{
    public bool IsActive { get; set; }
}

public sealed class ConcurCredentialsRequest
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string CompanyUuid { get; set; } = string.Empty;
}

public sealed class MaconomyCredentialsRequest
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class CreateMappingRequest
{
    public string SourceSystem { get; set; } = string.Empty;
    public string DestinationSystem { get; set; } = string.Empty;
    public string SourceField { get; set; } = string.Empty;
    public string DestinationField { get; set; } = string.Empty;
    public string? TransformationRule { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UpdateMappingRequest
{
    public string? DestinationField { get; set; }
    public string? TransformationRule { get; set; }
    public bool? IsActive { get; set; }
}

public sealed record TenantResponse(Guid TenantId, string Identifier, string Status);

public sealed record TenantSummary(Guid TenantId, string Name, string Identifier, string Status);

public sealed record CredentialIndicator(bool Configured);

public sealed record TenantDetail(
    Guid TenantId,
    string Name,
    string Identifier,
    string Status,
    CredentialIndicator ConcurConfig,
    CredentialIndicator MaconomyConfig);

public sealed record CredentialTestResponse(bool Connected, string Message);

public sealed record MappingResponse(
    Guid Id,
    string SourceSystem,
    string DestinationSystem,
    string SourceField,
    string DestinationField,
    string? TransformationRule,
    bool IsActive);
