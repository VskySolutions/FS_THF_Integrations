using EmsPortal.Application;
using EmsPortal.Infrastructure;
using EmsPortal.Infrastructure.Logging;
using Microsoft.Extensions.Hosting;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Structured logging to SQL Server, enriched with correlation ID, service, environment.
builder.Services.AddSerilog((_, loggerConfiguration) =>
    SerilogConfigurator.Configure(
        loggerConfiguration,
        builder.Configuration,
        "EmsPortal.McpServer",
        builder.Environment.EnvironmentName));

// Clean Architecture composition root. The MCP Server invokes Integration API
// flows over internal HTTP; tool registration and the MCP transport are added
// in later work orders.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var host = builder.Build();
host.Run();
