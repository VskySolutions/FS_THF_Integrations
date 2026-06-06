using IntegrationHub.Application.Abstractions.Retry;
using Microsoft.Extensions.Logging;

namespace IntegrationHub.Infrastructure.Retry;

/// <summary>
/// Placeholder executor used until the Connector Framework (Phase 2) provides the real
/// flow executor. Records the re-enqueue request so the retry scheduler is exercisable
/// end-to-end without the connector pipeline.
/// </summary>
internal sealed class PlaceholderIntegrationJobExecutor : IIntegrationJobExecutor
{
    private readonly ILogger<PlaceholderIntegrationJobExecutor> _logger;

    public PlaceholderIntegrationJobExecutor(ILogger<PlaceholderIntegrationJobExecutor> logger)
    {
        _logger = logger;
    }

    public Task EnqueueForExecutionAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Re-enqueue requested for job {JobId}; connector executor not yet wired (Phase 2)",
            jobId);
        return Task.CompletedTask;
    }
}
