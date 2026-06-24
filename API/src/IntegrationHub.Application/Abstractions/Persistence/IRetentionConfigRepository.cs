using IntegrationHub.Domain.Entities;

namespace IntegrationHub.Application.Abstractions.Persistence;

/// <summary>Data access for the per-tenant <see cref="DeletedRecordRetentionConfig"/>.</summary>
public interface IRetentionConfigRepository
{
    /// <summary>The retention config for a tenant (ambient tenant when <paramref name="tenantId"/> is null), or null when unset.</summary>
    Task<DeletedRecordRetentionConfig?> GetAsync(Guid? tenantId, CancellationToken cancellationToken = default);

    Task AddAsync(DeletedRecordRetentionConfig config, CancellationToken cancellationToken = default);

    void Update(DeletedRecordRetentionConfig config);
}
