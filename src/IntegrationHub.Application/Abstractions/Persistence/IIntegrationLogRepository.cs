using IntegrationHub.Domain.Entities;

namespace IntegrationHub.Application.Abstractions.Persistence;

/// <summary>
/// Data access for <see cref="IntegrationLog"/> records. Logs are written by the
/// Background Worker and read by the Integration API and MCP Server.
/// </summary>
public interface IIntegrationLogRepository
{
    Task AddAsync(IntegrationLog log, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IntegrationLog>> ListByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default);
}
