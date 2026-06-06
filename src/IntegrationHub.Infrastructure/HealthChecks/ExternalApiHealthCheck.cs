using IntegrationHub.Shared.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace IntegrationHub.Infrastructure.HealthChecks;

/// <summary>
/// Lightweight connectivity probe for an external system. Issues a short-timeout GET to
/// the configured base URL. Unconfigured systems report Degraded rather than failing the
/// overall readiness, so the platform can start before every connector is provisioned.
/// </summary>
internal abstract class ExternalApiHealthCheck : IHealthCheck
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    private readonly IHttpClientFactory _httpClientFactory;

    protected ExternalApiHealthCheck(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    protected abstract string SystemName { get; }

    protected abstract string BaseUrl { get; }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            return HealthCheckResult.Degraded($"{SystemName} API not configured.");
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(ProbeTimeout);

        try
        {
            var client = _httpClientFactory.CreateClient($"health:{SystemName}");
            using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

            // Any HTTP response (including 401/403) proves connectivity to the endpoint.
            return HealthCheckResult.Healthy($"{SystemName} API reachable ({(int)response.StatusCode}).");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"{SystemName} API unreachable.", ex);
        }
    }
}

internal sealed class PaycorApiHealthCheck : ExternalApiHealthCheck
{
    public const string Name = "paycor";
    private readonly PaycorOptions _options;

    public PaycorApiHealthCheck(IHttpClientFactory httpClientFactory, IOptions<PaycorOptions> options)
        : base(httpClientFactory) => _options = options.Value;

    protected override string SystemName => "Paycor";
    protected override string BaseUrl => _options.BaseUrl;
}

internal sealed class ConcurApiHealthCheck : ExternalApiHealthCheck
{
    public const string Name = "concur";
    private readonly ConcurOptions _options;

    public ConcurApiHealthCheck(IHttpClientFactory httpClientFactory, IOptions<ConcurOptions> options)
        : base(httpClientFactory) => _options = options.Value;

    protected override string SystemName => "Concur";
    protected override string BaseUrl => _options.BaseUrl;
}

internal sealed class MaconomyApiHealthCheck : ExternalApiHealthCheck
{
    public const string Name = "maconomy";
    private readonly MaconomyOptions _options;

    public MaconomyApiHealthCheck(IHttpClientFactory httpClientFactory, IOptions<MaconomyOptions> options)
        : base(httpClientFactory) => _options = options.Value;

    protected override string SystemName => "Maconomy";
    protected override string BaseUrl => _options.BaseUrl;
}
