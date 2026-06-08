namespace IntegrationHub.Shared.Configuration;

/// <summary>
/// Connection settings for an external system that the Background Worker integrates with.
/// Placeholder shape only — concrete connector wiring is delivered in a later work order.
/// </summary>
public class ExternalSystemOptions
{
    /// <summary>Base URL of the external system's API.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>OAuth2 / API client identifier.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth2 / API client secret. Injected via environment or secrets manager.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Per-request timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>Paycor-specific external system options.</summary>
public sealed class PaycorOptions : ExternalSystemOptions { }

/// <summary>Concur-specific external system options.</summary>
public sealed class ConcurOptions : ExternalSystemOptions { }

/// <summary>Maconomy-specific external system options.</summary>
public sealed class MaconomyOptions : ExternalSystemOptions { }
