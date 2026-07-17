namespace EmsPortal.Shared.Configuration;

/// <summary>
/// Configuration for the Hangfire background queue shared by the Integration API
/// (enqueue side) and the Background Worker (server side). Placeholder shape only.
/// </summary>
public sealed class HangfireOptions
{
    /// <summary>Number of concurrent worker threads on the Background Worker server.</summary>
    public int WorkerCount { get; set; } = 10;

    /// <summary>Whether the Hangfire dashboard is exposed by the host.</summary>
    public bool DashboardEnabled { get; set; } = true;

    /// <summary>Schema name used for the Hangfire SQL Server tables.</summary>
    public string SchemaName { get; set; } = "HangFire";

    /// <summary>Logical name of this Hangfire server instance. Empty uses the machine default.</summary>
    public string ServerName { get; set; } = string.Empty;
}
