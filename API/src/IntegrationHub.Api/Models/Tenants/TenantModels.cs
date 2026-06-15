namespace IntegrationHub.Api.Models.Tenants;

public sealed class CreateTenantRequest
{
    public string Name { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = "UTC";
}

public sealed class UpdateTenantRequest
{
    public string Name { get; set; } = string.Empty;
    public string? TimeZoneId { get; set; }
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

public sealed record TenantResponse(Guid TenantId, string Identifier, string Status);

public sealed record TenantSummary(
    Guid TenantId,
    string Name,
    string Identifier,
    string Status,
    string TimeZoneId,
    string? CreatedBy,
    string? UpdatedBy,
    DateTime CreatedOnUtc,
    DateTime UpdatedOnUtc);

public sealed record CredentialIndicator(bool Configured);

public sealed record TenantDetail(
    Guid TenantId,
    string Name,
    string Identifier,
    string Status,
    string TimeZoneId,
    CredentialIndicator ConcurConfig,
    CredentialIndicator MaconomyConfig,
    DateTime CreatedOnUtc,
    DateTime UpdatedOnUtc);

public sealed record CredentialTestResponse(bool Connected, string Message);
