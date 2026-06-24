using FluentValidation;
using FluentValidation.AspNetCore;
using global::Hangfire;
using IntegrationHub.Api.Filters;
using IntegrationHub.Api.Hangfire;
using IntegrationHub.Api.HealthChecks;
using IntegrationHub.Api.Logging;
using IntegrationHub.Api.Middleware;
using IntegrationHub.Api.OpenApi;
using IntegrationHub.Api.Security;
using IntegrationHub.Api.Tenancy;
using Scalar.AspNetCore;
using IntegrationHub.Application;
using IntegrationHub.Application.Abstractions.Tenancy;
using IntegrationHub.Infrastructure;
using IntegrationHub.Infrastructure.Hangfire;
using IntegrationHub.Infrastructure.HealthChecks;
using IntegrationHub.Infrastructure.Logging;
using IntegrationHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Structured logging to SQL Server, enriched with correlation ID, service, environment.
builder.Host.UseSerilog((context, _, loggerConfiguration) =>
    SerilogConfigurator.Configure(
        loggerConfiguration,
        context.Configuration,
        "IntegrationHub.Api",
        context.HostingEnvironment.EnvironmentName));

// API host services. Controllers route through the global validation filter, which emits
// the ApiResponseFactory.ValidationError envelope (ADR-002); the default [ApiController]
// 400 behavior is suppressed so our envelope is the single validation response shape.
builder.Services.AddControllers(options => options.Filters.Add<ValidationActionFilter>());
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(
    options => options.SuppressModelStateInvalidFilter = true);

// FluentValidation: auto-validate request DTOs into ModelState, which the
// ValidationActionFilter then turns into the ApiResponseFactory.ValidationError envelope.
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

builder.Services.AddIntegrationHubOpenApi();

// Authentication (JWT + API key), the AnyOf composite scheme, and RBAC policies.
builder.Services.AddIntegrationHubAuthentication(builder.Configuration);

// Hangfire storage so the API can host the monitoring dashboard (jobs run in the Worker).
builder.Services.AddIntegrationHubHangfireDashboard(builder.Configuration);

// Health checks: SQL Server + external system connectivity.
builder.Services.AddIntegrationHubHealthChecks();

// Clean Architecture composition root.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Universal Features (Phase 14): local-disk attachment storage under the content root.
builder.Services.AddScoped<IntegrationHub.Application.Abstractions.UniversalFeatures.IFileStorage, IntegrationHub.Api.Storage.LocalFileStorage>();

// Dashboard (WO-72): short-lived in-process cache (singleton) + scoped read-model aggregator.
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IntegrationHub.Api.Dashboard.IDashboardCacheService, IntegrationHub.Api.Dashboard.DashboardCacheService>();
builder.Services.AddScoped<IntegrationHub.Api.Dashboard.IDashboardQueryService, IntegrationHub.Api.Dashboard.DashboardQueryService>();

// CORS for the browser SPA (WEB/). Allowed origins come from configuration
// (Cors:AllowedOrigins); falls back to the local Quasar dev server ports.
const string SpaCorsPolicy = "SpaCors";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:9000", "http://localhost:9001"];
builder.Services.AddCors(options =>
    options.AddPolicy(SpaCorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()));

var app = builder.Build();

// The Integration API owns the application schema and applies EF Core migrations
// on startup. The Background Worker and MCP Server must not run migrations.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<IntegrationHubDbContext>();
    dbContext.Database.Migrate();

    // Seed a bootstrap Super Admin on first run so the platform is usable out of the box.
    await IntegrationHub.Api.Startup.BootstrapSeeder.SeedAsync(scope.ServiceProvider, builder.Configuration);
}

// OpenAPI spec (/openapi/v1.json) and the Scalar UI (/scalar/v1) are exposed only in
// Development and Staging.
if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("IntegrationHub API"));
}

// Correlation ID is established first so every downstream log entry — and the 500
// error body — carries it. Exception handling then wraps all downstream middleware,
// followed by request/response logging and auth.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestResponseLoggingMiddleware>();

// CORS runs before authentication so preflight OPTIONS requests succeed.
app.UseCors(SpaCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

// Resolve the active tenant from the authenticated JWT and populate ITenantContext.
app.UseMiddleware<TenantResolutionMiddleware>();

// Propagate the active tenant into Hangfire job payloads at enqueue time.
var httpContextAccessor = app.Services.GetRequiredService<IHttpContextAccessor>();
GlobalJobFilters.Filters.Add(new TenantHangfireJobFilter(() =>
{
    var tenant = httpContextAccessor.HttpContext?.RequestServices.GetService<ITenantContext>();
    return tenant?.IsResolved == true
        ? new TenantSnapshot(tenant.TenantId, tenant.TenantIdentifier)
        : (TenantSnapshot?)null;
}));

// Hangfire dashboard at /hangfire, gated by the TenantAdminOrAbove policy.
app.UseIntegrationHubHangfireDashboard(builder.Configuration);

// Health endpoints, all anonymous (exempt from authentication).
// /health: aggregate with per-component detail. /health/live: process liveness only.
// /health/ready: all dependency probes must be healthy.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteAsync,
}).AllowAnonymous();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthCheckResponseWriter.WriteAsync,
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(IntegrationHubHealthCheckExtensions.ReadyTag),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync,
}).AllowAnonymous();

// Controllers are delivered in later phases; routing is wired here.
app.MapControllers();

app.Run();

/// <summary>Exposed so the integration test host (WebApplicationFactory&lt;Program&gt;) can bootstrap the app.</summary>
public partial class Program;
