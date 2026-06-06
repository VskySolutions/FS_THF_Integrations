namespace IntegrationHub.Shared.Security;

/// <summary>
/// Names of the registered authentication schemes. JWT is the primary scheme for
/// interactive users; API key is the fallback for machine-to-machine callers.
/// </summary>
public static class AuthenticationSchemes
{
    public const string Jwt = "Jwt";
    public const string ApiKey = "ApiKey";

    /// <summary>Composite policy scheme accepting a request authenticated by either scheme.</summary>
    public const string AnyOf = "AnyOf";
}
