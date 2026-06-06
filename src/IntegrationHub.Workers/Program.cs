using IntegrationHub.Application;
using IntegrationHub.Infrastructure;
using IntegrationHub.Infrastructure.Logging;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Structured logging to SQL Server, enriched with correlation ID, service, environment.
builder.Services.AddSerilog((_, loggerConfiguration) =>
    SerilogConfigurator.Configure(
        loggerConfiguration,
        builder.Configuration,
        "IntegrationHub.Workers",
        builder.Environment.EnvironmentName));

// Clean Architecture composition root.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Hosted background services (Hangfire server, recurring jobs) are registered in later work orders.

var host = builder.Build();
host.Run();
