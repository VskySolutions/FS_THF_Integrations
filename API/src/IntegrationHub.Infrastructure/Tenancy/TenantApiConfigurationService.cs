using System.Text.Json;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Security;
using IntegrationHub.Application.Abstractions.Tenancy;
using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Infrastructure.Tenancy;

/// <summary>
/// Loads, decrypts, and deserializes the active tenant's external system credentials,
/// caching the result within the current DI scope. Returns null when none are configured.
/// </summary>
internal sealed class TenantApiConfigurationService : ITenantApiConfigurationService
{
    private readonly ITenantContext _tenantContext;
    private readonly ITenantApiConfigurationRepository _repository;
    private readonly ICredentialEncryptionService _encryptionService;

    private ConcurConfigDto? _concurCache;
    private MaconomyConfigDto? _maconomyCache;
    private bool _concurLoaded;
    private bool _maconomyLoaded;

    public TenantApiConfigurationService(
        ITenantContext tenantContext,
        ITenantApiConfigurationRepository repository,
        ICredentialEncryptionService encryptionService)
    {
        _tenantContext = tenantContext;
        _repository = repository;
        _encryptionService = encryptionService;
    }

    public async Task<ConcurConfigDto?> GetConcurConfigAsync(CancellationToken cancellationToken = default)
    {
        if (_concurLoaded)
        {
            return _concurCache;
        }

        _concurCache = await LoadAsync<ConcurConfigDto>(SystemName.Concur, cancellationToken);
        _concurLoaded = true;
        return _concurCache;
    }

    public async Task<MaconomyConfigDto?> GetMaconomyConfigAsync(CancellationToken cancellationToken = default)
    {
        if (_maconomyLoaded)
        {
            return _maconomyCache;
        }

        _maconomyCache = await LoadAsync<MaconomyConfigDto>(SystemName.Maconomy, cancellationToken);
        _maconomyLoaded = true;
        return _maconomyCache;
    }

    private async Task<T?> LoadAsync<T>(SystemName system, CancellationToken cancellationToken) where T : class
    {
        if (!_tenantContext.IsResolved)
        {
            return null;
        }

        var configuration = await _repository.GetAsync(_tenantContext.TenantId, system, cancellationToken);
        if (configuration is null || string.IsNullOrWhiteSpace(configuration.EncryptedCredentials))
        {
            return null;
        }

        var json = _encryptionService.Decrypt(configuration.EncryptedCredentials);
        return JsonSerializer.Deserialize<T>(json);
    }
}
