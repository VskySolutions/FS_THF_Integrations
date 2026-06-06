namespace IntegrationHub.Application.Abstractions.Tenancy;

/// <summary>Decrypted Concur API credentials for the active tenant.</summary>
public sealed record ConcurConfigDto(string ClientId, string ClientSecret, string BaseUrl, string CompanyUuid);

/// <summary>Decrypted Maconomy API credentials for the active tenant.</summary>
public sealed record MaconomyConfigDto(string BaseUrl, string Username, string Password);
