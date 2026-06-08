using IntegrationHub.Domain.Enums;
using IntegrationHub.Shared.Connectors;

namespace IntegrationHub.Application.Abstractions.Connectors;

/// <summary>
/// Orchestrates a complete integration flow in fixed order — fetch (source connector) →
/// validate → transform → push (target connector) — halting on the first failure and
/// reporting the outcome to the job tracking layer (REQ-COF-004).
/// </summary>
public interface IIntegrationService
{
    /// <summary>The flow this service implements.</summary>
    IntegrationFlowType FlowType { get; }

    /// <summary>Executes the flow end-to-end for the current tenant/job context.</summary>
    Task<IntegrationFlowResult> ExecuteAsync(CancellationToken cancellationToken = default);
}
