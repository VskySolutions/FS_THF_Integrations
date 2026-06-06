using IntegrationHub.Application;
using IntegrationHub.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// API host services. Controllers, authentication, middleware, and routing are
// added in later work orders.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Clean Architecture composition root.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();
