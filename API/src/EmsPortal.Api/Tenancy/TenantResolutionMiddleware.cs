using System.Security.Claims;
using System.Text.Json;
using EmsPortal.Api.Security;
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
        if (!Guid.TryParse(claimValue, out var claimTenantId) || claimTenantId == Guid.Empty)
        {
            _logger.LogWarning("Request without a resolvable activeTenantId claim");
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized,
                "Tenant could not be resolved.", ApiErrorCodes.Unauthorized);
            return;
        }

        // Super-Admin tenant override: administration screens let a Super Admin work in a tenant they hold
        // no assignment in, so the header re-points the ambient context and every tenant-scoped query
        // follows. Deliberately ONLY honoured for a Super Admin — for anyone else the header is ignored
        // outright rather than rejected, so a stale one in a browser can never widen their access. The
        // tenant still has to exist and be active: the checks below run against the overridden id.
        var tenantId = claimTenantId;
        if (TryReadTenantOverride(context, out var overrideTenantId) && overrideTenantId != tenantId)
        {
            if (context.User.IsSuperAdmin())
            {
                _logger.LogInformation(
                    "Super Admin tenant override: {ClaimTenantId} → {OverrideTenantId}", tenantId, overrideTenantId);
                tenantId = overrideTenantId;
            }
            else
            {
                _logger.LogWarning(
                    "Ignored tenant override header from a non-Super-Admin caller (claim {TenantId})", tenantId);
            }
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

        // Keep the PRINCIPAL in step with the context. Most tenant-scoped code reads the tenant from the
        // activeTenantId claim (User.GetActiveTenantId()) rather than from ITenantContext, and the two must
        // never disagree: a scoped Super Admin would otherwise list one tenant's rows through the ambient
        // filter while writing rows stamped with another. Rewriting the claim here means every one of those
        // call sites follows the override without being touched — and for a non-Super-Admin, where the
        // override is never applied, this is a no-op.
        if (tenant.Id != claimTenantId)
        {
            context.User = WithActiveTenantClaim(context.User, tenant.Id);
        }
        await _next(context);
    }

    /// <summary>
    /// The tenant the SPA is working in, sent on every request. Named "site" for historical reasons — site
    /// and tenant are the same identifier — and carries a Super Admin's tenant-scope selection when one is
    /// active. A header rather than a query string so it applies to every call without each endpoint
    /// having to accept and thread a parameter.
    /// </summary>
    public const string TenantOverrideHeader = "X-Site-Id";

    /// <summary>
    /// The caller with their <c>activeTenantId</c> claim swapped for the overridden tenant. Every other
    /// claim is carried over verbatim — roles and permissions in particular, which authorisation reads
    /// straight off the principal — and the identity's name/role claim types are preserved so
    /// <c>Identity.Name</c> and <c>IsInRole</c> keep working.
    /// </summary>
    private static ClaimsPrincipal WithActiveTenantClaim(ClaimsPrincipal principal, Guid tenantId)
    {
        var source = principal.Identity as ClaimsIdentity;
        var identity = new ClaimsIdentity(
            principal.Claims.Where(c => c.Type != ClaimTypeNames.ActiveTenantId),
            source?.AuthenticationType,
            source?.NameClaimType ?? ClaimsIdentity.DefaultNameClaimType,
            source?.RoleClaimType ?? ClaimsIdentity.DefaultRoleClaimType);

        identity.AddClaim(new Claim(ClaimTypeNames.ActiveTenantId, tenantId.ToString()));
        return new ClaimsPrincipal(identity);
    }

    private static bool TryReadTenantOverride(HttpContext context, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        return context.Request.Headers.TryGetValue(TenantOverrideHeader, out var raw)
            && Guid.TryParse(raw.ToString(), out tenantId)
            && tenantId != Guid.Empty;
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
