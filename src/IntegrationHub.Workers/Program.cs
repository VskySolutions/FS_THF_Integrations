using IntegrationHub.Application;
using IntegrationHub.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

// Clean Architecture composition root.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Hosted background services (Hangfire server, recurring jobs) are registered in later work orders.

var host = builder.Build();
host.Run();
