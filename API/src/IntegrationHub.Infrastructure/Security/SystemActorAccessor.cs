using IntegrationHub.Application.Abstractions.Security;

namespace IntegrationHub.Infrastructure.Security;

/// <summary>
/// Default actor accessor used by hosts without an HTTP request context (the
/// Background Worker and MCP Server). The API overrides this with an
/// HttpContext-based accessor that resolves the authenticated user.
/// </summary>
internal sealed class SystemActorAccessor : IActorAccessor
{
    public const string SystemIdentity = "system";

    public string GetCurrentActor() => SystemIdentity;
}
