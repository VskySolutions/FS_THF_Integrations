namespace IntegrationHub.Application.Abstractions.Retry;

/// <summary>
/// Re-enqueues an integration job for execution on the background queue. The concrete
/// executor that runs the connector flow is delivered with the Connector Framework
/// (Phase 2); until then a placeholder implementation records the re-enqueue request.
/// </summary>
public interface IIntegrationJobExecutor
{
    Task EnqueueForExecutionAsync(Guid jobId, CancellationToken cancellationToken = default);
}
