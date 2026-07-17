using EmsPortal.Domain.Enums;

namespace EmsPortal.Domain.Entities;

/// <summary>
/// Marks an entity property as field-level change-tracked (Universal Features — Modified Log). The
/// startup <c>TrackedFieldRegistry</c> scans for this attribute to build the tracked-field catalogue,
/// and the <c>FieldChangeInterceptor</c> records a <see cref="FieldModifiedLog"/> whenever the property
/// changes. System-tracked fields are always on; optional fields can be toggled per tenant.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class TrackedFieldAttribute : Attribute
{
    public TrackedFieldAttribute(EntityType entityType, string displayName, bool isSystemTracked = false)
    {
        EntityType = entityType;
        DisplayName = displayName;
        IsSystemTracked = isSystemTracked;
    }

    /// <summary>The Universal Features entity type this property belongs to.</summary>
    public EntityType EntityType { get; }

    /// <summary>Human-readable field name shown in the change-history UI (e.g. "Credit Limit").</summary>
    public string DisplayName { get; }

    /// <summary>True for always-on System Tracked fields; false for tenant-toggleable optional fields.</summary>
    public bool IsSystemTracked { get; }
}
