using EmsPortal.Application.Abstractions.OptionSets;
using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Application.Abstractions.Tenancy;
using EmsPortal.Domain.Entities;

namespace EmsPortal.Application.OptionSets;

/// <summary>
/// Default <see cref="IOptionSetService"/>. Writes are pinned to the resolved tenant. A standard (seeded)
/// list is a starting point rather than a fixed one: its values can be added, renamed, re-ordered and
/// removed. Only deleting the list itself is refused, since feature code references its key.
/// </summary>
public sealed class OptionSetService : IOptionSetService
{
    private readonly IOptionSetRepository _sets;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantContext _tenantContext;

    public OptionSetService(IOptionSetRepository sets, IUnitOfWork unitOfWork, ITenantContext tenantContext)
    {
        _sets = sets;
        _unitOfWork = unitOfWork;
        _tenantContext = tenantContext;
    }

    private Guid TenantId =>
        _tenantContext.IsResolved
            ? _tenantContext.TenantId
            : throw new OptionSetException(OptionSetErrorCodes.NoActiveTenant, "No active tenant for the caller.");

    public async Task<OptionSet> CreateSetAsync(CreateOptionSetInput input, CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var key = input.Key.Trim();

        if (await _sets.KeyExistsAsync(tenantId, input.EntityType, key, excludeId: null, cancellationToken))
        {
            throw new OptionSetException(OptionSetErrorCodes.DuplicateKey, $"A list with the key '{key}' already exists for this entity.");
        }

        var set = new OptionSet
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityType = input.EntityType,
            Key = key,
            Name = input.Name.Trim(),
            ParentSetId = input.ParentSetId,
            ItemSortMode = input.ItemSortMode,
            IsSystem = false,
            IsActive = true,
        };

        await _sets.AddSetAsync(set, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return set;
    }

    public async Task<OptionSet?> UpdateSetAsync(Guid id, UpdateOptionSetInput input, CancellationToken cancellationToken = default)
    {
        var set = await _sets.GetSetWithItemsAsync(id, TenantId, cancellationToken);
        if (set is null)
        {
            return null;
        }

        set.Name = input.Name.Trim();
        set.ItemSortMode = input.ItemSortMode;
        set.IsActive = input.IsActive;

        _sets.UpdateSet(set);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return set;
    }

    public async Task<bool?> DeleteSetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var set = await _sets.GetSetWithItemsAsync(id, TenantId, cancellationToken);
        if (set is null)
        {
            return null;
        }

        EnsureDeletable(set);

        _sets.RemoveSet(set);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<OptionSetItem?> CreateItemAsync(CreateOptionItemInput input, CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var set = await _sets.GetSetWithItemsAsync(input.OptionSetId, tenantId, cancellationToken);
        if (set is null)
        {
            return null;
        }

        var value = input.Value.Trim();
        if (await _sets.ItemValueExistsAsync(set.Id, tenantId, value, excludeId: null, cancellationToken))
        {
            throw new OptionSetException(OptionSetErrorCodes.DuplicateValue, $"A value '{value}' already exists in this list.");
        }

        // New custom-ordered items go to the end of the list.
        var nextOrder = set.Items.Count == 0 ? 0 : set.Items.Max(i => i.SortOrder) + 1;

        var item = new OptionSetItem
        {
            Id = Guid.NewGuid(),
            OptionSetId = set.Id,
            // Match the parent list: a value added to a standard (platform) list belongs to it exactly as
            // the seeded values do, rather than being stamped with whichever tenant happened to add it.
            TenantId = set.TenantId ?? tenantId,
            ParentItemId = input.ParentItemId,
            Value = value,
            Label = input.Label.Trim(),
            Description = NullIfBlank(input.Description),
            SortOrder = nextOrder,
            IsDefault = input.IsDefault,
            IsActive = true,
            BackgroundColor = NullIfBlank(input.BackgroundColor),
            TextColor = NullIfBlank(input.TextColor),
            MetadataJson = NullIfBlank(input.MetadataJson),
        };

        await _sets.AddItemAsync(item, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task<OptionSetItem?> UpdateItemAsync(Guid setId, Guid itemId, UpdateOptionItemInput input, CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var set = await _sets.GetSetWithItemsAsync(setId, tenantId, cancellationToken);
        if (set is null)
        {
            return null;
        }

        var item = await _sets.GetItemAsync(itemId, setId, cancellationToken);
        if (item is null)
        {
            return null;
        }

        var value = input.Value.Trim();
        if (await _sets.ItemValueExistsAsync(setId, item.TenantId, value, excludeId: itemId, cancellationToken))
        {
            throw new OptionSetException(OptionSetErrorCodes.DuplicateValue, $"A value '{value}' already exists in this list.");
        }

        item.Value = value;
        item.Label = input.Label.Trim();
        item.Description = NullIfBlank(input.Description);
        item.ParentItemId = input.ParentItemId;
        item.IsDefault = input.IsDefault;
        item.IsActive = input.IsActive;
        item.BackgroundColor = NullIfBlank(input.BackgroundColor);
        item.TextColor = NullIfBlank(input.TextColor);
        item.MetadataJson = NullIfBlank(input.MetadataJson);

        _sets.UpdateItem(item);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task<bool?> DeleteItemAsync(Guid setId, Guid itemId, CancellationToken cancellationToken = default)
    {
        var set = await _sets.GetSetWithItemsAsync(setId, TenantId, cancellationToken);
        if (set is null)
        {
            return null;
        }

        var item = await _sets.GetItemAsync(itemId, setId, cancellationToken);
        if (item is null)
        {
            return null;
        }

        _sets.RemoveItem(item);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool?> ReorderItemsAsync(Guid setId, IReadOnlyList<Guid> orderedItemIds, CancellationToken cancellationToken = default)
    {
        var set = await _sets.GetSetWithItemsAsync(setId, TenantId, cancellationToken);
        if (set is null)
        {
            return null;
        }

        var items = await _sets.ListItemsAsync(setId, cancellationToken);
        var byId = items.ToDictionary(i => i.Id);

        // The new order must reference exactly the set's items — no missing, extra, or unknown ids.
        if (orderedItemIds.Count != items.Count || orderedItemIds.Distinct().Count() != orderedItemIds.Count
            || orderedItemIds.Any(id => !byId.ContainsKey(id)))
        {
            throw new OptionSetException(OptionSetErrorCodes.InvalidReorder, "The reorder request must list every item in the set exactly once.");
        }

        for (var index = 0; index < orderedItemIds.Count; index++)
        {
            var item = byId[orderedItemIds[index]];
            if (item.SortOrder != index)
            {
                item.SortOrder = index;
                _sets.UpdateItem(item);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Guards against DELETING a platform-standard (seeded) list. Their values are editable — a standard
    /// list is a starting point, not a fixed one — but removing the list itself is not offered: the key is
    /// referenced by feature code (REMS.Department, REMS.Status …), and the seeder would simply recreate
    /// it on the next restart.
    /// </summary>
    private static void EnsureDeletable(OptionSet set)
    {
        if (set.TenantId is null || set.IsSystem)
        {
            throw new OptionSetException(OptionSetErrorCodes.ReadOnlyStandardSet, "A standard list cannot be deleted. Its values can be edited instead.");
        }
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
