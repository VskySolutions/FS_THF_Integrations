using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Application.Abstractions.Tenancy;
using EmsPortal.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EmsPortal.Infrastructure.Persistence.Repositories;

/// <summary>
/// Generic Deleted Records Management over the soft-deletable tenant entities. Restores and hard-deletes
/// cascade the Universal Feature rows that share the record's <c>(EntityType, EntityId)</c> key. Currently
/// implemented for <see cref="EntityType.CustomerRequest"/> and <see cref="EntityType.UserGroup"/>; other
/// types report unsupported until their identity projection is added.
/// </summary>
internal sealed class DeletedRecordsRepository : IDeletedRecordsRepository
{
    private static readonly EntityType[] Supported = { EntityType.CustomerRequest, EntityType.UserGroup };

    private readonly EmsPortalDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public DeletedRecordsRepository(EmsPortalDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public bool IsSupported(EntityType entityType) => Supported.Contains(entityType);

    private Guid? Effective(Guid? tenantId) => tenantId ?? (_tenantContext.IsResolved ? _tenantContext.TenantId : (Guid?)null);

    public async Task<(IReadOnlyList<DeletedRecordRow> Items, int Total)> ListDeletedAsync(
        EntityType entityType, Guid? tenantId, int page, int limit, CancellationToken cancellationToken = default)
    {
        var tenant = Effective(tenantId);
        switch (entityType)
        {
            case EntityType.CustomerRequest:
            {
                var query = _dbContext.CustomerRequests.IgnoreQueryFilters().Where(c => c.Deleted);
                if (tenant is { } t) query = query.Where(c => c.TenantId == t);
                var ordered = query.OrderByDescending(c => c.DeletedOnUtc);
                var total = await ordered.CountAsync(cancellationToken);
                var items = await ordered.Skip((page - 1) * limit).Take(limit)
                    .Select(c => new DeletedRecordRow(c.Id, c.CustomerRequestNumber ?? c.CompanyName, c.TenantId, c.UpdatedById, c.DeletedOnUtc))
                    .ToListAsync(cancellationToken);
                return (items, total);
            }

            case EntityType.UserGroup:
            {
                var query = _dbContext.UserGroups.IgnoreQueryFilters().Where(g => g.Deleted);
                if (tenant is { } t) query = query.Where(g => g.TenantId == t);
                var ordered = query.OrderByDescending(g => g.DeletedOnUtc);
                var total = await ordered.CountAsync(cancellationToken);
                var items = await ordered.Skip((page - 1) * limit).Take(limit)
                    .Select(g => new DeletedRecordRow(g.Id, g.Name, g.TenantId, g.UpdatedById, g.DeletedOnUtc))
                    .ToListAsync(cancellationToken);
                return (items, total);
            }

            default:
                return (Array.Empty<DeletedRecordRow>(), 0);
        }
    }

    public async Task<string?> GetDeletedIdentityAsync(EntityType entityType, Guid entityId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = Effective(tenantId);
        return entityType switch
        {
            EntityType.CustomerRequest => await _dbContext.CustomerRequests.IgnoreQueryFilters()
                .Where(c => c.Id == entityId && c.Deleted && (tenant == null || c.TenantId == tenant))
                .Select(c => c.CustomerRequestNumber ?? c.CompanyName).FirstOrDefaultAsync(cancellationToken),
            EntityType.UserGroup => await _dbContext.UserGroups.IgnoreQueryFilters()
                .Where(g => g.Id == entityId && g.Deleted && (tenant == null || g.TenantId == tenant))
                .Select(g => g.Name).FirstOrDefaultAsync(cancellationToken),
            _ => null,
        };
    }

    public async Task<bool> RestoreAsync(EntityType entityType, Guid entityId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = Effective(tenantId);
        var restored = entityType switch
        {
            EntityType.CustomerRequest => await _dbContext.CustomerRequests.IgnoreQueryFilters()
                .Where(c => c.Id == entityId && c.Deleted && (tenant == null || c.TenantId == tenant))
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Deleted, false).SetProperty(c => c.DeletedOnUtc, (DateTime?)null), cancellationToken),
            EntityType.UserGroup => await _dbContext.UserGroups.IgnoreQueryFilters()
                .Where(g => g.Id == entityId && g.Deleted && (tenant == null || g.TenantId == tenant))
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.Deleted, false).SetProperty(g => g.DeletedOnUtc, (DateTime?)null), cancellationToken),
            _ => 0,
        };

        if (restored == 0)
        {
            return false;
        }

        await RestoreUniversalFeaturesAsync(entityType, entityId, cancellationToken);
        return true;
    }

    public async Task<bool> HardDeleteAsync(EntityType entityType, Guid entityId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = Effective(tenantId);
        var deleted = entityType switch
        {
            EntityType.CustomerRequest => await _dbContext.CustomerRequests.IgnoreQueryFilters()
                .Where(c => c.Id == entityId && (tenant == null || c.TenantId == tenant)).ExecuteDeleteAsync(cancellationToken),
            EntityType.UserGroup => await _dbContext.UserGroups.IgnoreQueryFilters()
                .Where(g => g.Id == entityId && (tenant == null || g.TenantId == tenant)).ExecuteDeleteAsync(cancellationToken),
            _ => 0,
        };

        if (deleted == 0)
        {
            return false;
        }

        await CascadeDeleteUniversalFeaturesAsync(entityType, entityId, cancellationToken);
        return true;
    }

    public async Task<IReadOnlyDictionary<EntityType, int>> CountOverdueAsync(int retentionDays, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = Effective(tenantId);
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var result = new Dictionary<EntityType, int>();

        var customerQuery = _dbContext.CustomerRequests.IgnoreQueryFilters().Where(c => c.Deleted && c.DeletedOnUtc <= cutoff);
        if (tenant is { } t1) customerQuery = customerQuery.Where(c => c.TenantId == t1);
        result[EntityType.CustomerRequest] = await customerQuery.CountAsync(cancellationToken);

        var groupQuery = _dbContext.UserGroups.IgnoreQueryFilters().Where(g => g.Deleted && g.DeletedOnUtc <= cutoff);
        if (tenant is { } t2) groupQuery = groupQuery.Where(g => g.TenantId == t2);
        result[EntityType.UserGroup] = await groupQuery.CountAsync(cancellationToken);

        return result;
    }

    /// <summary>Un-deletes the soft-deleted UF rows that share the record's (EntityType, EntityId) key.</summary>
    private async Task RestoreUniversalFeaturesAsync(EntityType entityType, Guid entityId, CancellationToken cancellationToken)
    {
        await _dbContext.Notes.IgnoreQueryFilters().Where(n => n.EntityType == entityType && n.EntityId == entityId && n.Deleted)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.Deleted, false).SetProperty(n => n.DeletedOnUtc, (DateTime?)null), cancellationToken);
        await _dbContext.EntityTags.IgnoreQueryFilters().Where(e => e.EntityType == entityType && e.EntityId == entityId && e.Deleted)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Deleted, false).SetProperty(e => e.DeletedOnUtc, (DateTime?)null), cancellationToken);
        await _dbContext.Attachments.IgnoreQueryFilters().Where(a => a.EntityType == entityType && a.EntityId == entityId && a.Deleted)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Deleted, false).SetProperty(a => a.DeletedOnUtc, (DateTime?)null), cancellationToken);
        await _dbContext.Pins.IgnoreQueryFilters().Where(p => p.EntityType == entityType && p.EntityId == entityId && p.Deleted)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Deleted, false).SetProperty(p => p.DeletedOnUtc, (DateTime?)null), cancellationToken);
        await _dbContext.ColourCodes.IgnoreQueryFilters().Where(c => c.EntityType == entityType && c.EntityId == entityId && c.Deleted)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Deleted, false).SetProperty(c => c.DeletedOnUtc, (DateTime?)null), cancellationToken);
        await _dbContext.Checklists.IgnoreQueryFilters().Where(c => c.EntityType == entityType && c.EntityId == entityId && c.Deleted)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Deleted, false).SetProperty(c => c.DeletedOnUtc, (DateTime?)null), cancellationToken);
    }

    /// <summary>Permanently removes all UF rows for the record's (EntityType, EntityId) key. Child rows cascade via DB FKs.</summary>
    private async Task CascadeDeleteUniversalFeaturesAsync(EntityType entityType, Guid entityId, CancellationToken cancellationToken)
    {
        await _dbContext.Notes.IgnoreQueryFilters().Where(n => n.EntityType == entityType && n.EntityId == entityId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.EntityTags.IgnoreQueryFilters().Where(e => e.EntityType == entityType && e.EntityId == entityId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Attachments.IgnoreQueryFilters().Where(a => a.EntityType == entityType && a.EntityId == entityId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.ActivityEvents.IgnoreQueryFilters().Where(a => a.EntityType == entityType && a.EntityId == entityId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Reminders.IgnoreQueryFilters().Where(r => r.EntityType == entityType && r.EntityId == entityId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Checklists.IgnoreQueryFilters().Where(c => c.EntityType == entityType && c.EntityId == entityId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Pins.IgnoreQueryFilters().Where(p => p.EntityType == entityType && p.EntityId == entityId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.ColourCodes.IgnoreQueryFilters().Where(c => c.EntityType == entityType && c.EntityId == entityId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.FieldModifiedLogs.IgnoreQueryFilters().Where(l => l.EntityType == entityType && l.EntityId == entityId).ExecuteDeleteAsync(cancellationToken);
    }
}
