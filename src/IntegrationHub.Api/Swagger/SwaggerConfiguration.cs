using System.Reflection;
using IntegrationHub.Shared.Security;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace IntegrationHub.Api.Swagger;

/// <summary>
/// Swagger/OpenAPI configuration (WO-31): documents the JWT Bearer and API Key schemes,
/// wires XML comments from the API and Application assemblies, groups by controller tag,
/// and annotates common responses. UI exposure is gated to Development/Staging in Program.cs.
/// </summary>
public static class SwaggerConfiguration
{
    public static IServiceCollection AddIntegrationHubSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "IntegrationHub API",
                Version = "v1",
                Description = "Integration platform API: Concur import triggers, administration, tenants, users, and auth.",
            });

            // Platform-issued JWT (RS256) and machine-to-machine API key.
            options.AddSecurityDefinition(AuthenticationSchemes.Jwt, new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Platform-issued JWT bearer token.",
            });
            options.AddSecurityDefinition(AuthenticationSchemes.ApiKey, new OpenApiSecurityScheme
            {
                Name = "X-Api-Key",
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Description = "Machine-to-machine API key.",
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [Ref(AuthenticationSchemes.Jwt)] = Array.Empty<string>(),
                [Ref(AuthenticationSchemes.ApiKey)] = Array.Empty<string>(),
            });

            options.OperationFilter<CommonResponsesOperationFilter>();

            // XML comments from the API and Application assemblies.
            IncludeXmlComments(options, Assembly.GetExecutingAssembly());
            IncludeXmlComments(options, typeof(IntegrationHub.Application.DependencyInjection).Assembly);
        });

        return services;
    }

    private static OpenApiSecurityScheme Ref(string id) => new()
    {
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = id },
    };

    private static void IncludeXmlComments(SwaggerGenOptions options, Assembly assembly)
    {
        var path = Path.Combine(AppContext.BaseDirectory, $"{assembly.GetName().Name}.xml");
        if (File.Exists(path))
        {
            options.IncludeXmlComments(path, includeControllerXmlComments: true);
        }
    }
}

/// <summary>Documents the common error responses on every operation (400/401/403/500).</summary>
public sealed class CommonResponsesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Responses.TryAdd("400", new OpenApiResponse { Description = "Validation error (ApiErrorResponse)." });
        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Unauthenticated." });
        operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Forbidden." });
        operation.Responses.TryAdd("500", new OpenApiResponse { Description = "Unexpected error (ApiErrorResponse)." });
    }
}
