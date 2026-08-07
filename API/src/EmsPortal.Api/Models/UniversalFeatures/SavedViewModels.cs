namespace EmsPortal.Api.Models.UniversalFeatures;

/// <summary>Request to create a saved list view.</summary>
public sealed class CreateSavedViewRequest
{
    public string Name { get; set; } = string.Empty;
    public string ListPage { get; set; } = string.Empty;
    public string? FiltersJson { get; set; }
    public string? SortJson { get; set; }
    public string? ColumnsJson { get; set; }
    public bool IsShared { get; set; }
}

/// <summary>Request to update a saved list view.</summary>
public sealed class UpdateSavedViewRequest
{
    public string Name { get; set; } = string.Empty;
    public string? FiltersJson { get; set; }
    public string? SortJson { get; set; }
    public string? ColumnsJson { get; set; }
    public bool IsShared { get; set; }
}

/// <summary>A shared saved view with its owner, for the admin management page.</summary>
public sealed record SharedSavedViewResponse(
    Guid Id, string Name, string ListPage, Guid? OwnerId, string? OwnerName, DateTime CreatedOnUtc,
    // The audit trail every list offers as hidden-by-default columns. CreatedBy is distinct from
    // OwnerName: the owner is whose view it is, CreatedBy is who last wrote the row.
    string? CreatedBy = null, string? UpdatedBy = null, DateTime? UpdatedOnUtc = null);

/// <summary>A saved view as returned to the client.</summary>
public sealed record SavedViewResponse(
    Guid Id,
    string Name,
    string ListPage,
    string? FiltersJson,
    string? SortJson,
    string? ColumnsJson,
    bool IsShared,
    bool IsOwner,
    Guid? OwnerId);
