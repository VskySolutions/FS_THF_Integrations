using EmsPortal.Application.Abstractions.Security;
using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace EmsPortal.Infrastructure.Persistence.ModifiedLog;

/// <summary>
/// EF Core save interceptor that captures field-level change history (Universal Features — Modified Log).
/// Before each save it inspects modified entities for <c>[TrackedField]</c> properties registered in the
/// <see cref="TrackedFieldRegistry"/>, and stages a <see cref="FieldModifiedLog"/> for each real change —
/// committed in the same transaction as the originating save. System Tracked fields are always captured;
/// optional fields are skipped when disabled for the tenant via <see cref="ModifiedLogFieldConfig"/>.
/// </summary>
public sealed class FieldChangeInterceptor : SaveChangesInterceptor
{
    private readonly IActorAccessor _actorAccessor;
    private readonly IFieldValueFormatter _formatter;

    public FieldChangeInterceptor(IActorAccessor actorAccessor, IFieldValueFormatter formatter)
    {
        _actorAccessor = actorAccessor;
        _formatter = formatter;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context)
        {
            var candidates = Collect(context);
            if (candidates.Count > 0)
            {
                var disabled = QueryDisabledOptional(context, candidates);
                AddLogs(context, candidates, disabled);
            }
        }

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
        {
            var candidates = Collect(context);
            if (candidates.Count > 0)
            {
                var disabled = await QueryDisabledOptionalAsync(context, candidates, cancellationToken);
                AddLogs(context, candidates, disabled);
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private List<Candidate> Collect(DbContext context)
    {
        var candidates = new List<Candidate>();
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Modified)
            {
                continue;
            }

            var descriptors = TrackedFieldRegistry.ForClrType(entry.Entity.GetType());
            if (descriptors.Count == 0)
            {
                continue;
            }

            foreach (var descriptor in descriptors)
            {
                var property = entry.Property(descriptor.PropertyName);
                if (!property.IsModified || Equals(property.OriginalValue, property.CurrentValue))
                {
                    continue;
                }

                candidates.Add(new Candidate(
                    descriptor,
                    ReadGuid(entry, "Id"),
                    ReadGuid(entry, "TenantId"),
                    _formatter.Format(property.OriginalValue),
                    _formatter.Format(property.CurrentValue)));
            }
        }

        return candidates;
    }

    private void AddLogs(DbContext context, IReadOnlyList<Candidate> candidates, HashSet<string> disabledOptional)
    {
        var actorId = Guid.TryParse(_actorAccessor.GetCurrentActor(), out var id) ? id : (Guid?)null;
        var now = DateTime.UtcNow;

        foreach (var candidate in candidates)
        {
            if (!candidate.Descriptor.IsSystemTracked && disabledOptional.Contains(DisabledKey(candidate.TenantId, candidate.Descriptor)))
            {
                continue;
            }

            context.Add(new FieldModifiedLog
            {
                Id = Guid.NewGuid(),
                TenantId = candidate.TenantId,
                EntityType = candidate.Descriptor.EntityType,
                EntityId = candidate.EntityId,
                FieldName = candidate.Descriptor.PropertyName,
                OldValue = candidate.OldValue,
                NewValue = candidate.NewValue,
                ChangedById = actorId,
                ChangedOnUtc = now,
            });
        }
    }

    private static HashSet<string> QueryDisabledOptional(DbContext context, IReadOnlyList<Candidate> candidates)
    {
        var optional = candidates.Where(c => !c.Descriptor.IsSystemTracked).ToList();
        if (optional.Count == 0)
        {
            return new HashSet<string>();
        }

        var rows = context.Set<ModifiedLogFieldConfig>().IgnoreQueryFilters()
            .Where(c => !c.IsEnabled && !c.Deleted)
            .Select(c => new { c.TenantId, c.EntityType, c.FieldName })
            .ToList();
        return rows.Select(r => $"{r.TenantId}|{r.EntityType}|{r.FieldName}").ToHashSet();
    }

    private static async Task<HashSet<string>> QueryDisabledOptionalAsync(DbContext context, IReadOnlyList<Candidate> candidates, CancellationToken cancellationToken)
    {
        var optional = candidates.Where(c => !c.Descriptor.IsSystemTracked).ToList();
        if (optional.Count == 0)
        {
            return new HashSet<string>();
        }

        var rows = await context.Set<ModifiedLogFieldConfig>().IgnoreQueryFilters()
            .Where(c => !c.IsEnabled && !c.Deleted)
            .Select(c => new { c.TenantId, c.EntityType, c.FieldName })
            .ToListAsync(cancellationToken);
        return rows.Select(r => $"{r.TenantId}|{r.EntityType}|{r.FieldName}").ToHashSet();
    }

    private static string DisabledKey(Guid tenantId, TrackedFieldDescriptor descriptor)
        => $"{tenantId}|{descriptor.EntityType}|{descriptor.PropertyName}";

    private static Guid ReadGuid(EntityEntry entry, string propertyName)
    {
        var property = entry.Metadata.FindProperty(propertyName);
        return property is not null && entry.Property(propertyName).CurrentValue is Guid value ? value : Guid.Empty;
    }

    private sealed record Candidate(TrackedFieldDescriptor Descriptor, Guid EntityId, Guid TenantId, string? OldValue, string? NewValue);
}
