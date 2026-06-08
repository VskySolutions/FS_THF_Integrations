using System.Net.Http.Headers;
using System.Net.Http.Json;
using IntegrationHub.Application.Abstractions.Connectors.Concur;
using IntegrationHub.Application.Abstractions.Security;
using IntegrationHub.Application.Abstractions.Tenancy;
using IntegrationHub.Shared.Connectors;
using Microsoft.Extensions.Logging;

namespace IntegrationHub.Infrastructure.Connectors.Concur;

/// <summary>
/// <see cref="IConcurConnector"/> implementation. Resolves per-tenant credentials from
/// <see cref="ITenantApiConfigurationService"/>, authenticates with Concur via OAuth2
/// (token cached in-memory per instance), fetches records, normalizes HTTP errors into
/// <see cref="ConnectorResult{T}"/>, and structured-logs every outbound call.
/// </summary>
internal sealed class ConcurConnector : IConcurConnector
{
    private const string SystemLabel = "Concur";
    private const string HttpClientName = "Concur";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITenantApiConfigurationService _configurationService;
    private readonly ICorrelationContext _correlationContext;
    private readonly ILogger<ConcurConnector> _logger;

    private string? _accessToken;
    private DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;

    public ConcurConnector(
        IHttpClientFactory httpClientFactory,
        ITenantApiConfigurationService configurationService,
        ICorrelationContext correlationContext,
        ILogger<ConcurConnector> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configurationService = configurationService;
        _correlationContext = correlationContext;
        _logger = logger;
    }

    public async Task<ConnectorResult<bool>> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        var config = await _configurationService.GetConcurConfigAsync(cancellationToken);
        if (config is null)
        {
            return ConnectorResult<bool>.Fail("Concur credentials are not configured for the tenant.", isRetriable: false);
        }

        var client = CreateClient(config.BaseUrl);
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = config.ClientId,
            ["client_secret"] = config.ClientSecret,
            ["company_uuid"] = config.CompanyUuid,
        });

        _logger.LogInformation("Concur AuthenticateAsync CorrelationId={CorrelationId}", _correlationContext.CorrelationId);

        try
        {
            using var response = await client.PostAsync("oauth2/v0/token", form, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return ConnectorError.FromStatusCode<bool>(SystemLabel, "Authenticate", response.StatusCode, body);
            }

            var token = await response.Content.ReadFromJsonAsync<ConcurTokenResponse>(cancellationToken: cancellationToken);
            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                return ConnectorResult<bool>.Fail("Concur returned an empty access token.", isRetriable: true);
            }

            _accessToken = token.AccessToken;
            // Refresh a minute early to avoid edge-of-expiry failures.
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(0, token.ExpiresIn - 60));
            return ConnectorResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return ConnectorError.FromException<bool>(SystemLabel, "Authenticate", ex);
        }
    }

    public Task<ConnectorResult<IReadOnlyList<ConcurExpenseReport>>> GetApprovedExpenseReportsAsync(CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<ConcurExpenseReport>>("expensereports?status=approved", "GetApprovedExpenseReports", cancellationToken);

    public Task<ConnectorResult<IReadOnlyList<ConcurVendorInvoice>>> GetVendorInvoicesAsync(CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<ConcurVendorInvoice>>("invoices", "GetVendorInvoices", cancellationToken);

    public Task<ConnectorResult<IReadOnlyList<ConcurVendorPayment>>> GetVendorPaymentsAsync(CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<ConcurVendorPayment>>("payments", "GetVendorPayments", cancellationToken);

    private async Task<ConnectorResult<T>> GetAsync<T>(string path, string operation, CancellationToken cancellationToken)
    {
        var config = await _configurationService.GetConcurConfigAsync(cancellationToken);
        if (config is null)
        {
            return ConnectorResult<T>.Fail("Concur credentials are not configured for the tenant.", isRetriable: false);
        }

        var token = await EnsureTokenAsync(cancellationToken);
        if (!token.Success)
        {
            return ConnectorResult<T>.Fail(token.ErrorMessage ?? "Concur authentication failed.", token.IsRetriable);
        }

        var client = CreateClient(config.BaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        _logger.LogInformation(
            "Concur {Operation} GET {Path} CorrelationId={CorrelationId}",
            operation, path, _correlationContext.CorrelationId);

        try
        {
            using var response = await client.GetAsync(path, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Concur {Operation} failed {StatusCode} CorrelationId={CorrelationId}",
                    operation, (int)response.StatusCode, _correlationContext.CorrelationId);
                return ConnectorError.FromStatusCode<T>(SystemLabel, operation, response.StatusCode, body);
            }

            var payload = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
            return payload is null
                ? ConnectorResult<T>.Fail($"Concur {operation} returned an empty body.", isRetriable: true)
                : ConnectorResult<T>.Ok(payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Concur {Operation} threw CorrelationId={CorrelationId}", operation, _correlationContext.CorrelationId);
            return ConnectorError.FromException<T>(SystemLabel, operation, ex);
        }
    }

    private async Task<ConnectorResult<bool>> EnsureTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_accessToken) && DateTimeOffset.UtcNow < _tokenExpiresAt)
        {
            return ConnectorResult<bool>.Ok(true);
        }

        return await AuthenticateAsync(cancellationToken);
    }

    private HttpClient CreateClient(string baseUrl)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
        return client;
    }

    private sealed class ConcurTokenResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }
}
