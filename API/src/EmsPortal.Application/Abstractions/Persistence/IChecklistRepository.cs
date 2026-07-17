using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;

namespace EmsPortal.Application.Abstractions.Persistence;

/// <summary>Data access for <see cref="Checklist"/>s and their <see cref="ChecklistItem"/>s.</summary>
public interface IChecklistRepository
{
    Task AddAsync(Checklist checklist, CancellationToken cancellationToken = default);

    /// <summary>Loads a checklist with its items by id.</summary>
    Task<Checklist?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Remove(Checklist checklist);

    /// <summary>All checklists (with items) for an entity record.</summary>
    Task<IReadOnlyList<Checklist>> ListAsync(EntityType entityType, Guid entityId, CancellationToken cancellationToken = default);

    Task AddItemAsync(ChecklistItem item, CancellationToken cancellationToken = default);

    Task<ChecklistItem?> GetItemAsync(Guid itemId, CancellationToken cancellationToken = default);

    void UpdateItem(ChecklistItem item);

    void RemoveItem(ChecklistItem item);
}
