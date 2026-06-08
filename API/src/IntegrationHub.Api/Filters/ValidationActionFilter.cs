using FluentValidation.Results;
using IntegrationHub.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace IntegrationHub.Api.Filters;

/// <summary>
/// Intercepts invalid model state before a controller action executes and returns an
/// <c>ApiResponseFactory.ValidationError()</c> envelope as HTTP 400 (ADR-002). Registered
/// globally; works with <c>[ApiController]</c> when the default 400 behavior is suppressed.
/// </summary>
public sealed class ValidationActionFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid)
        {
            return;
        }

        var failures = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .SelectMany(entry => entry.Value!.Errors
                .Select(error => new ValidationFailure(entry.Key, error.ErrorMessage)));

        context.Result = new BadRequestObjectResult(ApiResponseFactory.ValidationError(failures));
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
