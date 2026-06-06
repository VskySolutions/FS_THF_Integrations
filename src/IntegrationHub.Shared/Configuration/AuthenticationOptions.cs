namespace IntegrationHub.Shared.Configuration;

/// <summary>
/// Authentication configuration for the Integration API. Supports JWT bearer,
/// OAuth2 client credentials, and API key headers. Placeholder shape only —
/// the authentication middleware is delivered in a later work order.
/// </summary>
public sealed class AuthenticationOptions
{
    /// <summary>Expected token issuer.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Expected token audience.</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>Signing key for symmetric JWT validation. Injected via secrets at deploy time.</summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Name of the HTTP header carrying the API key.</summary>
    public string ApiKeyHeaderName { get; set; } = "X-Api-Key";
}
