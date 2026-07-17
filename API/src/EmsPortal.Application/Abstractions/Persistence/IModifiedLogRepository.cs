using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;

namespace EmsPortal.Application.Abstractions.Persistence;

/// <summary>Read access to the <see cref="FieldModifiedLog"/> and CRUD for tenant field config.</summary>
public interface IModifiedLogRepository
{
    /// <summary>Paginated, newest-first change history for a specific (entity, field).</summary>
    Task<(IReadOnlyList<FieldModifiedLog> Items, int Total)> ListAsync(
        EntityType entityType, Guid entityId, string fieldName, int page, int limit, CancellationToken cancellationToken = default);

    /// <summary>Map of field name → change count for a record (only fields with at least one entry).</summary>
    Task<IReadOnlyDictionary<string, int>> GetIconCountsAsync(EntityType entityType, Guid entityId, CancellationToken cancellationToken = default);

    // ---- Tenant field configuration ----
    Task<IReadOnlyList<ModifiedLogFieldConfig>> GetConfigsAsync(CancellationToken cancellationToken = default);

    Task<ModifiedLogFieldConfig?> GetConfigAsync(EntityType entityType, string fieldName, CancellationToken cancellationToken = default);

    Task AddConfigAsync(ModifiedLogFieldConfig config, CancellationToken cancellationToken = default);

    void UpdateConfig(ModifiedLogFieldConfig config);
}
