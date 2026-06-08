namespace IntegrationHub.Application.Abstractions.Security;

/// <summary>
/// Scoped service holding the correlation ID for the current request or job. Set by
/// CorrelationIdMiddleware (API) or the job dispatcher (Worker) and injected by all
/// components for log enrichment and outbound header propagation (REQ-INF-006).
/// </summary>
public interface ICorrelationContext
{
    /// <summary>The correlation ID for the current scope. Empty until set.</summary>
    string CorrelationId { get; }

    /// <summary>Sets the correlation ID for the current scope.</summary>
    void Set(string correlationId);
}
