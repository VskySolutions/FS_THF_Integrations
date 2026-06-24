using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Api.Models.UniversalFeatures;

/// <summary>Request to create a checklist with optional initial items.</summary>
public sealed class CreateChecklistRequest
{
    public EntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public string Title { get; set; } = string.Empty;
    public List<string>? Items { get; set; }
}

/// <summary>Request to add an item to a checklist.</summary>
public sealed class AddChecklistItemRequest
{
    public string Text { get; set; } = string.Empty;
}

/// <summary>Request to edit a checklist item's text.</summary>
public sealed class UpdateChecklistItemRequest
{
    public string Text { get; set; } = string.Empty;
}

/// <summary>Request to toggle a checklist item's completion.</summary>
public sealed class ToggleChecklistItemRequest
{
    public bool IsCompleted { get; set; }
}

/// <summary>Request to reorder a checklist's items (ids in the new order).</summary>
public sealed class ReorderChecklistRequest
{
    public List<Guid> ItemIds { get; set; } = new();
}

/// <summary>A checklist item as returned to the client.</summary>
public sealed record ChecklistItemResponse(
    Guid Id, string Text, bool IsCompleted, Guid? CompletedById, DateTime? CompletedOnUtc, int SortOrder);

/// <summary>A checklist with its items and progress.</summary>
public sealed record ChecklistResponse(
    Guid Id,
    EntityType EntityType,
    Guid EntityId,
    string Title,
    int CompletedCount,
    int TotalCount,
    IReadOnlyList<ChecklistItemResponse> Items);
