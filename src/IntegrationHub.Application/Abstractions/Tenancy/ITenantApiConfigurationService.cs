namespace IntegrationHub.Application.Abstractions.Tenancy;

/// <summary>
/// Resolves and decrypts the active tenant's external system credentials (Multi-Tenancy).
/// Returns null when no credentials are configured for the tenant — connectors treat this
/// as a non-retriable failure (CREDENTIALS_NOT_CONFIGURED). Results are cached within the
/// current DI scope to avoid repeated decryption during a single job.
/// </summary>
public interface ITenantApiConfigurationService
{
    Task<ConcurConfigDto?> GetConcurConfigAsync(CancellationToken cancellationToken = default);

    Task<MaconomyConfigDto?> GetMaconomyConfigAsync(CancellationToken cancellationToken = default);
}
