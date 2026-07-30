using System.Reflection;
using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;

namespace EmsPortal.Infrastructure.Persistence.ModifiedLog;

/// <summary>One registered tracked field discovered from a <see cref="TrackedFieldAttribute"/>.</summary>
/// <param name="Key">Stable key of the form <c>"{EntityType}.{PropertyName}"</c>.</param>
/// <param name="EntityClrType">The declaring entity CLR type.</param>
/// <param name="PropertyName">The property name (stored as the log's FieldName).</param>
/// <param name="EntityType">The Universal Features entity type.</param>
/// <param name="DisplayName">Human-readable label.</param>
/// <param name="IsSystemTracked">True for always-on system fields; false for tenant-toggleable fields.</param>
public sealed record TrackedFieldDescriptor(
    string Key,
    Type EntityClrType,
    string PropertyName,
    EntityType EntityType,
    string DisplayName,
    bool IsSystemTracked);

/// <summary>
/// Static, in-process catalogue of <c>[TrackedField]</c>-decorated entity properties, built once by
/// reflecting over the Domain assembly at first use. The <c>FieldChangeInterceptor</c> consults it to
/// decide which property changes to log; the Modified Log admin API uses it to list configurable fields.
/// </summary>
public static class TrackedFieldRegistry
{
    private static readonly IReadOnlyList<TrackedFieldDescriptor> Descriptors = Build();
    private static readonly Dictionary<Type, List<TrackedFieldDescriptor>> ByClrType =
        Descriptors.GroupBy(d => d.EntityClrType).ToDictionary(g => g.Key, g => g.ToList());
    private static readonly Dictionary<string, TrackedFieldDescriptor> ByKey =
        Descriptors.ToDictionary(d => d.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>All registered tracked fields.</summary>
    public static IReadOnlyList<TrackedFieldDescriptor> All => Descriptors;

    /// <summary>Registered fields declared on the given entity CLR type (empty when none).</summary>
    public static IReadOnlyList<TrackedFieldDescriptor> ForClrType(Type clrType)
        => ByClrType.TryGetValue(clrType, out var list) ? list : Array.Empty<TrackedFieldDescriptor>();

    /// <summary>Registered fields for the given Universal Features entity type.</summary>
    public static IReadOnlyList<TrackedFieldDescriptor> ForEntityType(EntityType entityType)
        => Descriptors.Where(d => d.EntityType == entityType).ToList();

    /// <summary>Looks up a field by its stable key, or null when not registered.</summary>
    public static TrackedFieldDescriptor? GetByKey(string key)
        => ByKey.TryGetValue(key, out var descriptor) ? descriptor : null;

    private static IReadOnlyList<TrackedFieldDescriptor> Build()
    {
        var result = new List<TrackedFieldDescriptor>();
        foreach (var type in typeof(OptionSet).Assembly.GetTypes())
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetCustomAttribute<TrackedFieldAttribute>() is { } attribute)
                {
                    result.Add(new TrackedFieldDescriptor(
                        $"{attribute.EntityType}.{property.Name}",
                        type,
                        property.Name,
                        attribute.EntityType,
                        attribute.DisplayName,
                        attribute.IsSystemTracked));
                }
            }
        }

        return result;
    }
}
