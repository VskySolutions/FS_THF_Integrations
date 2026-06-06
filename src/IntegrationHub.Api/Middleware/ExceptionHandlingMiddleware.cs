using System.Text.Json;
using IntegrationHub.Application.Abstractions.Security;

namespace IntegrationHub.Api.Middleware;

/// <summary>
/// Catches unhandled exceptions before they reach the caller, logs the full exception
/// (type, message, stack trace, correlation ID) via Serilog, and returns a structured
/// HTTP 500 carrying only the correlation ID and a generic message. Exception detail is
/// exposed only in Development (Error Handling &amp; Retry blueprint).
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private const string GenericMessage = "An unexpected error occurred";

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context, ICorrelationContext correlationContext)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var correlationId = correlationContext.CorrelationId;
            _logger.LogError(
                ex,
                "Unhandled exception {ExceptionType} CorrelationId={CorrelationId}",
                ex.GetType().FullName,
                correlationId);

            if (context.Response.HasStarted)
            {
                // Headers already flushed — cannot rewrite the response.
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var body = new Dictionary<string, string?>
            {
                ["correlationId"] = correlationId,
                ["message"] = GenericMessage,
            };

            if (_environment.IsDevelopment())
            {
                body["detail"] = ex.Message;
                body["exceptionType"] = ex.GetType().FullName;
            }

            await context.Response.WriteAsync(JsonSerializer.Serialize(body), context.RequestAborted);
        }
    }
}
