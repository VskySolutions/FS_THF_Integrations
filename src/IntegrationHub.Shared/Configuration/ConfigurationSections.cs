namespace IntegrationHub.Shared.Configuration;

/// <summary>
/// Canonical names of the <c>appsettings.json</c> configuration sections used across
/// the IntegrationHub platform. Centralizing the section names here keeps binding code
/// (in the Infrastructure layer) and the configuration templates in sync.
/// </summary>
public static class ConfigurationSections
{
    /// <summary>Connection string key for the shared SQL Server database.</summary>
    public const string SqlServerConnection = "SqlServer";

    /// <summary>Hangfire background queue configuration.</summary>
    public const string Hangfire = "Hangfire";

    /// <summary>Serilog structured logging configuration.</summary>
    public const string Serilog = "Serilog";

    /// <summary>Authentication (JWT / OAuth2 / API key) configuration.</summary>
    public const string Authentication = "Authentication";

    /// <summary>Paycor external system configuration.</summary>
    public const string Paycor = "ExternalSystems:Paycor";

    /// <summary>Concur external system configuration.</summary>
    public const string Concur = "ExternalSystems:Concur";

    /// <summary>Maconomy external system configuration.</summary>
    public const string Maconomy = "ExternalSystems:Maconomy";
}
