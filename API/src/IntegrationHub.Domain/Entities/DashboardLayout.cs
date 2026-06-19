using System.Text.Json;

namespace IntegrationHub.Domain.Entities;

/// <summary>
/// A single user's personalised dashboard layout: the widget order plus the sets of hidden and
/// collapsed widgets. Per-user (not tenant-scoped); one active row per user. The widget sets are
/// stored as JSON-array strings with parsed <c>[NotMapped]</c> helpers.
/// </summary>
public class DashboardLayout : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning user (unique, one layout per user).</summary>
    public Guid UserId { get; set; }

    /// <summary>Ordered widget keys, stored as a JSON array string.</summary>
    public string WidgetOrderJson { get; set; } = "[]";

    /// <summary>Hidden widget keys, stored as a JSON array string.</summary>
    public string HiddenWidgetsJson { get; set; } = "[]";

    /// <summary>Collapsed widget keys, stored as a JSON array string.</summary>
    public string CollapsedWidgetsJson { get; set; } = "[]";

    /// <summary>Parsed view of <see cref="WidgetOrderJson"/>.</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public IReadOnlyList<string> WidgetOrder => Parse(WidgetOrderJson);

    /// <summary>Parsed view of <see cref="HiddenWidgetsJson"/>.</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public IReadOnlyList<string> HiddenWidgets => Parse(HiddenWidgetsJson);

    /// <summary>Parsed view of <see cref="CollapsedWidgetsJson"/>.</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public IReadOnlyList<string> CollapsedWidgets => Parse(CollapsedWidgetsJson);

    private static IReadOnlyList<string> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
