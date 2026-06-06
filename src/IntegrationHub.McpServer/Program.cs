using IntegrationHub.Application;
using IntegrationHub.Infrastructure;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Clean Architecture composition root. The MCP Server invokes Integration API
// flows over internal HTTP; tool registration and the MCP transport are added
// in later work orders.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var host = builder.Build();
host.Run();
