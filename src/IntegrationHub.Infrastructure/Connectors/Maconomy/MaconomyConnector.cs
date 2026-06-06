using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using IntegrationHub.Application.Abstractions.Connectors.Maconomy;
using IntegrationHub.Application.Abstractions.Security;
using IntegrationHub.Application.Abstractions.Tenancy;
using IntegrationHub.Shared.Connectors;
using Microsoft.Extensions.Logging;

namespace IntegrationHub.Infrastructure.Connectors.Maconomy;

/// <summary>
/// <see cref="IMaconomyConnector"/> implementation. Resolves per-tenant credentials,
/// authenticates with Maconomy (session token cached in-memory), performs writes with
/// GET-before-POST duplicate detection where applicable, normalizes HTTP errors into
/// <see cref="ConnectorResult{T}"/>, and structured-logs every outbound call.
/// </summary>
internal sealed class MaconomyConnector : IMaconomyConnector
{
    private const string SystemLabel = "Maconomy";
    private const string HttpClientName = "Maconomy";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITenantApiConfigurationService _configurationService;
    private readonly ICorrelationContext _correlationContext;
    private readonly ILogger<MaconomyConnector> _logger;

    private string? _sessionToken;
    private DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;

    public MaconomyConnector(
        IHttpClientFactory httpClientFactory,
        ITenantApiConfigurationService configurationService,
        ICorrelationContext correlationContext,
        ILogger<MaconomyConnector> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configurationService = configurationService;
        _correlationContext = correlationContext;
        _logger = logger;
    }

    public async Task<ConnectorResult<bool>> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        var config = await _configurationService.GetMaconomyConfigAsync(cancellationToken);
        if (config is null)
        {
            return ConnectorResult<bool>.Fail("Maconomy credentials are not configured for the tenant.", isRetriable: false);
        }

        var client = CreateClient(config.BaseUrl);
        var credentials = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{config.Username}:{config.Password}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        _logger.LogInformation("Maconomy AuthenticateAsync CorrelationId={CorrelationId}", _correlationContext.CorrelationId);

        try
        {
            using var response = await client.PostAsync("auth/login", content: null, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return ConnectorError.FromStatusCode<bool>(SystemLabel, "Authenticate", response.StatusCode, body);
            }

            var token = await response.Content.ReadFromJsonAsync<MaconomyTokenResponse>(cancellationToken: cancellationToken);
            if (token is null || string.IsNullOrWhiteSpace(token.Token))
            {
                return ConnectorResult<bool>.Fail("Maconomy returned an empty session token.", isRetriable: true);
            }

            _sessionToken = token.Token;
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(0, token.ExpiresIn - 60));
            return ConnectorResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return ConnectorError.FromException<bool>(SystemLabel, "Authenticate", ex);
        }
    }

    public async Task<ConnectorResult<MaconomyEmployee?>> GetEmployeeAsync(string employeeId, CancellationToken cancellationToken = default)
    {
        var client = await CreateAuthenticatedClientAsync(cancellationToken);
        if (!client.Success)
        {
            return ConnectorResult<MaconomyEmployee?>.Fail(client.ErrorMessage!, client.IsRetriable);
        }

        _logger.LogInformation(
            "Maconomy GetEmployee {EmployeeId} CorrelationId={CorrelationId}", employeeId, _correlationContext.CorrelationId);

        try
        {
            using var response = await client.Payload!.GetAsync($"employees/{Uri.EscapeDataString(employeeId)}", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return ConnectorResult<MaconomyEmployee?>.Ok(null);
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return ConnectorError.FromStatusCode<MaconomyEmployee?>(SystemLabel, "GetEmployee", response.StatusCode, body);
            }

            var employee = await response.Content.ReadFromJsonAsync<MaconomyEmployee>(cancellationToken: cancellationToken);
            return ConnectorResult<MaconomyEmployee?>.Ok(employee);
        }
        catch (Exception ex)
        {
            return ConnectorError.FromException<MaconomyEmployee?>(SystemLabel, "GetEmployee", ex);
        }
    }

    public async Task<ConnectorResult<MaconomyWriteResult>> CreateEmployeeAsync(MaconomyEmployee employee, CancellationToken cancellationToken = default)
    {
        // Duplicate detection: GET before POST (at-least-once delivery safety net).
        var existing = await GetEmployeeAsync(employee.EmployeeId, cancellationToken);
        if (!existing.Success)
        {
            return ConnectorResult<MaconomyWriteResult>.Fail(existing.ErrorMessage!, existing.IsRetriable);
        }

        if (existing.Payload is not null)
        {
            _logger.LogWarning(
                "Maconomy CreateEmployee skipped — {EmployeeId} already exists CorrelationId={CorrelationId}",
                employee.EmployeeId, _correlationContext.CorrelationId);
            return ConnectorResult<MaconomyWriteResult>.Ok(new MaconomyWriteResult(employee.EmployeeId, Duplicate: true));
        }

        return await PostAsync("employees", employee, employee.EmployeeId, "CreateEmployee", cancellationToken);
    }

    public Task<ConnectorResult<MaconomyWriteResult>> UpdateEmployeeAsync(MaconomyEmployee employee, CancellationToken cancellationToken = default)
        => PutAsync($"employees/{Uri.EscapeDataString(employee.EmployeeId)}", employee, employee.EmployeeId, "UpdateEmployee", cancellationToken);

    public Task<ConnectorResult<MaconomyWriteResult>> UpdateEmployeeStatusAsync(string employeeId, string status, CancellationToken cancellationToken = default)
        => PutAsync($"employees/{Uri.EscapeDataString(employeeId)}/status", new { status }, employeeId, "UpdateEmployeeStatus", cancellationToken);

    public Task<ConnectorResult<MaconomyWriteResult>> WriteTimesheetAsync(MaconomyTimesheet timesheet, CancellationToken cancellationToken = default)
        => PostAsync("timesheets", timesheet, timesheet.EmployeeId, "WriteTimesheet", cancellationToken);

    public Task<ConnectorResult<MaconomyWriteResult>> WriteReimbursementAsync(MaconomyReimbursement reimbursement, CancellationToken cancellationToken = default)
        => PostAsync("reimbursements", reimbursement, reimbursement.EmployeeId, "WriteReimbursement", cancellationToken);

    public Task<ConnectorResult<MaconomyWriteResult>> WriteExpenseReportAsync(MaconomyExpenseReport report, CancellationToken cancellationToken = default)
        => PostAsync("expensereports", report, report.ReportId, "WriteExpenseReport", cancellationToken);

    public Task<ConnectorResult<MaconomyWriteResult>> WriteVendorInvoiceAsync(MaconomyVendorInvoice invoice, CancellationToken cancellationToken = default)
        => PostAsync("vendorinvoices", invoice, invoice.InvoiceNumber, "WriteVendorInvoice", cancellationToken);

    public Task<ConnectorResult<MaconomyWriteResult>> WriteVendorPaymentAsync(MaconomyVendorPayment payment, CancellationToken cancellationToken = default)
        => PostAsync("vendorpayments", payment, payment.PaymentId, "WriteVendorPayment", cancellationToken);

    private Task<ConnectorResult<MaconomyWriteResult>> PostAsync<TPayload>(
        string path, TPayload payload, string entityId, string operation, CancellationToken cancellationToken)
        => SendAsync(HttpMethod.Post, path, payload, entityId, operation, cancellationToken);

    private Task<ConnectorResult<MaconomyWriteResult>> PutAsync<TPayload>(
        string path, TPayload payload, string entityId, string operation, CancellationToken cancellationToken)
        => SendAsync(HttpMethod.Put, path, payload, entityId, operation, cancellationToken);

    private async Task<ConnectorResult<MaconomyWriteResult>> SendAsync<TPayload>(
        HttpMethod method, string path, TPayload payload, string entityId, string operation, CancellationToken cancellationToken)
    {
        var client = await CreateAuthenticatedClientAsync(cancellationToken);
        if (!client.Success)
        {
            return ConnectorResult<MaconomyWriteResult>.Fail(client.ErrorMessage!, client.IsRetriable);
        }

        _logger.LogInformation(
            "Maconomy {Operation} {Method} {Path} CorrelationId={CorrelationId}",
            operation, method.Method, path, _correlationContext.CorrelationId);

        try
        {
            using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(payload) };
            using var response = await client.Payload!.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Maconomy {Operation} failed {StatusCode} CorrelationId={CorrelationId}",
                    operation, (int)response.StatusCode, _correlationContext.CorrelationId);
                return ConnectorError.FromStatusCode<MaconomyWriteResult>(SystemLabel, operation, response.StatusCode, body);
            }

            return ConnectorResult<MaconomyWriteResult>.Ok(new MaconomyWriteResult(entityId, Duplicate: false));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Maconomy {Operation} threw CorrelationId={CorrelationId}", operation, _correlationContext.CorrelationId);
            return ConnectorError.FromException<MaconomyWriteResult>(SystemLabel, operation, ex);
        }
    }

    private async Task<ConnectorResult<HttpClient>> CreateAuthenticatedClientAsync(CancellationToken cancellationToken)
    {
        var config = await _configurationService.GetMaconomyConfigAsync(cancellationToken);
        if (config is null)
        {
            return ConnectorResult<HttpClient>.Fail("Maconomy credentials are not configured for the tenant.", isRetriable: false);
        }

        if (string.IsNullOrEmpty(_sessionToken) || DateTimeOffset.UtcNow >= _tokenExpiresAt)
        {
            var auth = await AuthenticateAsync(cancellationToken);
            if (!auth.Success)
            {
                return ConnectorResult<HttpClient>.Fail(auth.ErrorMessage ?? "Maconomy authentication failed.", auth.IsRetriable);
            }
        }

        var client = CreateClient(config.BaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _sessionToken);
        return ConnectorResult<HttpClient>.Ok(client);
    }

    private HttpClient CreateClient(string baseUrl)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
        return client;
    }

    private sealed class MaconomyTokenResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("token")]
        public string Token { get; init; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }
}
