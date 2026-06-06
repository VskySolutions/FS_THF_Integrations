using IntegrationHub.Api.Hangfire;
using IntegrationHub.Api.Logging;
using IntegrationHub.Api.Middleware;
using IntegrationHub.Api.Security;
using IntegrationHub.Application;
using IntegrationHub.Infrastructure;
using IntegrationHub.Infrastructure.Logging;
using IntegrationHub.Infrastructure.Persistence;
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

// API host services. Controllers and routing are added in later work orders.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Authentication (JWT + API key), the AnyOf composite scheme, and RBAC policies.
builder.Services.AddIntegrationHubAuthentication(builder.Configuration);

// Hangfire storage so the API can host the monitoring dashboard (jobs run in the Worker).
builder.Services.AddIntegrationHubHangfireDashboard(builder.Configuration);

// Clean Architecture composition root.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// The Integration API owns the application schema and applies EF Core migrations
// on startup. The Background Worker and MCP Server must not run migrations.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<IntegrationHubDbContext>();
    dbContext.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Correlation ID is established first so every downstream log entry — and the 500
// error body — carries it. Exception handling then wraps all downstream middleware,
// followed by request/response logging and auth.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestResponseLoggingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

// Hangfire dashboard at /hangfire, gated by the TenantAdminOrAbove policy.
app.UseIntegrationHubHangfireDashboard(builder.Configuration);

app.Run();
