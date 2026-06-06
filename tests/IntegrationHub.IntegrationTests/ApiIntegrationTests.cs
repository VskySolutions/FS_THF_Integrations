using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace IntegrationHub.IntegrationTests;

// WO-35: API integration tests against the real app + database (WebApplicationFactory).
[Collection("Api")]
public class ApiIntegrationTests
{
    private readonly IntegrationHubApiFactory _factory;

    public ApiIntegrationTests(IntegrationHubApiFactory factory) => _factory = factory;

    private async Task<string> LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = IntegrationHubApiFactory.AdminEmail, password = IntegrationHubApiFactory.AdminPassword });
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
    }

    [Fact]
    public async Task Health_returns_200_with_component_statuses()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("sqlserver").And.Contain("status");
    }

    [Fact]
    public async Task Concur_import_without_auth_is_401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/concur/expenses/import", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Concur_import_with_auth_returns_202_and_jobId()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync("/api/concur/expenses/import", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var jobId = doc.RootElement.GetProperty("data").GetProperty("jobId").GetString();
        Guid.TryParse(jobId, out _).Should().BeTrue();
    }

    [Fact]
    public async Task Admin_retry_unknown_job_returns_404()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/admin/retry/{Guid.NewGuid()}", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Admin_jobs_requires_auth()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/admin/jobs");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
