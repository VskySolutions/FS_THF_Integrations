using FluentAssertions;
using IntegrationHub.Api.Security;
using IntegrationHub.Api.Tenancy;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Infrastructure.Security;
using IntegrationHub.Infrastructure.Tenancy;
using IntegrationHub.Shared.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;

namespace IntegrationHub.UnitTests;

// WO-32: CorrelationIdMiddleware.
public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task Generates_a_new_correlation_id_when_header_absent()
    {
        var context = new DefaultHttpContext();
        var correlation = new CorrelationContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, correlation);

        Guid.TryParse(correlation.CorrelationId, out _).Should().BeTrue();
    }

    [Fact]
    public async Task Uses_existing_correlation_id_header()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "trace-123";
        var correlation = new CorrelationContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, correlation);

        correlation.CorrelationId.Should().Be("trace-123");
    }
}

// WO-42: TenantResolutionMiddleware.
public class TenantResolutionMiddlewareTests
{
    private static DefaultHttpContext AuthenticatedContext(Guid? activeTenantId)
    {
        var claims = new List<Claim>();
        if (activeTenantId is { } t)
        {
            claims.Add(new Claim(ClaimTypeNames.ActiveTenantId, t.ToString()));
        }

        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static TenantResolutionMiddleware Middleware(RequestDelegate next)
        => new(next, NullLogger<TenantResolutionMiddleware>.Instance);

    [Fact]
    public async Task Anonymous_request_passes_through_without_tenant()
    {
        var called = false;
        var context = new DefaultHttpContext(); // no authenticated user
        var tenantContext = new TenantContext();

        await Middleware(_ => { called = true; return Task.CompletedTask; })
            .InvokeAsync(context, tenantContext, Mock.Of<ITenantRepository>());

        called.Should().BeTrue();
        tenantContext.IsResolved.Should().BeFalse();
    }

    [Fact]
    public async Task Active_tenant_is_resolved_and_request_proceeds()
    {
        var tenant = TestData.Tenant(status: TenantStatus.Active);
        var context = AuthenticatedContext(tenant.Id);
        var tenantContext = new TenantContext();
        var repo = new Mock<ITenantRepository>();
        repo.Setup(r => r.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        var called = false;

        await Middleware(_ => { called = true; return Task.CompletedTask; })
            .InvokeAsync(context, tenantContext, repo.Object);

        called.Should().BeTrue();
        tenantContext.IsResolved.Should().BeTrue();
        tenantContext.TenantId.Should().Be(tenant.Id);
    }

    [Fact]
    public async Task Inactive_tenant_is_rejected_403_before_handler()
    {
        var tenant = TestData.Tenant(status: TenantStatus.Inactive);
        var context = AuthenticatedContext(tenant.Id);
        var tenantContext = new TenantContext();
        var repo = new Mock<ITenantRepository>();
        repo.Setup(r => r.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        var called = false;

        await Middleware(_ => { called = true; return Task.CompletedTask; })
            .InvokeAsync(context, tenantContext, repo.Object);

        called.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Missing_tenant_claim_is_unauthorized_401()
    {
        var context = AuthenticatedContext(activeTenantId: null);
        var tenantContext = new TenantContext();

        await Middleware(_ => Task.CompletedTask)
            .InvokeAsync(context, tenantContext, Mock.Of<ITenantRepository>());

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }
}
