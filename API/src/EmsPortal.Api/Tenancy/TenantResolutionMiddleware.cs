using System.Text.Json;
using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Application.Abstractions.Tenancy;
using EmsPortal.Domain.Enums;
using EmsPortal.Shared.Contracts;
using EmsPortal.Shared.Security;

namespace EmsPortal.Api.Tenancy;

/// <summary>
/// Resolves the active tenant for authenticated requests from the JWT <c>activeTenantId</c>
/// claim, validates it against the database, and populates <see cref="ITenantContext"/>
/// (Multi-Tenancy). Anonymous requests (swagger) pass through untouched.
/// Inactive tenants are rejected 403 (<c>TENANT_INACTIVE</c>); missing/unresolvable
/// tenants are rejected 401 (<c>UNAUTHORIZED</c>).
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, ITenantRepository tenantRepository)
    {
        // Anonymous endpoints (swagger) carry no tenant — let them through.
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var claimValue = context.User.FindFirst(ClaimTypeNames.ActiveTenantId)?.Value;
        if (!Guid.TryParse(claimValue, out var tenantId) || tenantId == Guid.Empty)
        {
            _logger.LogWarning("Request without a resolvable activeTenantId claim");
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized,
                "Tenant could not be resolved.", ApiErrorCodes.Unauthorized);
            return;
        }

        var tenant = await tenantRepository.GetByIdAsync(tenantId, context.RequestAborted);
        if (tenant is null)
        {
            _logger.LogWarning("activeTenantId {TenantId} does not resolve to a tenant", tenantId);
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized,
                "Tenant could not be resolved.", ApiErrorCodes.Unauthorized);
            return;
        }

        if (tenant.Status != TenantStatus.Active)
        {
            _logger.LogWarning("Request for inactive tenant {TenantId} (status {Status})", tenantId, tenant.Status);
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden,
                "The tenant is inactive.", ApiErrorCodes.TenantInactive);
            return;
        }

        tenantContext.Set(tenant.Id, tenant.Identifier);
        await _next(context);
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string message, string code)
    {
        var envelope = ApiResponseFactory.Error(code, message, details: string.Empty);
        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(envelope, SerializerOptions), context.RequestAborted);
    }
}
