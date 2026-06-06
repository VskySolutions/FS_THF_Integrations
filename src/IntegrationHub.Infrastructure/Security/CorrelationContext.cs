using IntegrationHub.Application.Abstractions.Security;

namespace IntegrationHub.Infrastructure.Security;

/// <summary>
/// Scoped, mutable correlation context. One instance per request scope (API) or job
/// scope (Worker).
/// </summary>
internal sealed class CorrelationContext : ICorrelationContext
{
    public string CorrelationId { get; private set; } = string.Empty;

    public void Set(string correlationId)
        => CorrelationId = correlationId ?? string.Empty;
}
