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

    // WO-39 (REQ-ADM-013): admin password reset.
    [Fact]
    public async Task Admin_reset_password_returns_temporary_password()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Scope the new user to an existing tenant (the bootstrap "system" tenant).
        var tenantsResponse = await client.GetAsync("/api/admin/tenants?page=1&limit=1");
        tenantsResponse.EnsureSuccessStatusCode();
        using var tenantsDoc = JsonDocument.Parse(await tenantsResponse.Content.ReadAsStringAsync());
        var tenantId = tenantsDoc.RootElement.GetProperty("data")[0].GetProperty("tenantId").GetString();

        var email = $"reset-{Guid.NewGuid():N}@test.local";
        var createResponse = await client.PostAsJsonAsync("/api/admin/users",
            new { email, displayName = "Reset Target", role = "Operator", tenantId });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        using var createDoc = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var userId = createDoc.RootElement.GetProperty("data").GetProperty("userId").GetString();

        var resetResponse = await client.PostAsync($"/api/admin/users/{userId}/reset-password", content: null);

        resetResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var resetDoc = JsonDocument.Parse(await resetResponse.Content.ReadAsStringAsync());
        var temporaryPassword = resetDoc.RootElement.GetProperty("data").GetProperty("temporaryPassword").GetString();
        temporaryPassword.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Admin_reset_password_requires_auth()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync($"/api/admin/users/{Guid.NewGuid()}/reset-password", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // WO/RBAC Phase 1: seeded system roles.
    [Fact]
    public async Task Roles_list_returns_seeded_system_roles()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/roles");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("SuperAdmin").And.Contain("TenantAdmin").And.Contain("Operator");
    }

    [Fact]
    public async Task Admin_reset_password_unknown_user_returns_404()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/admin/users/{Guid.NewGuid()}/reset-password", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
