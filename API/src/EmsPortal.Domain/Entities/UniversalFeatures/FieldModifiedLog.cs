using EmsPortal.Domain.Enums;

namespace EmsPortal.Domain.Entities;

/// <summary>
/// An append-only record of a single tracked field change, captured automatically by
/// <c>FieldChangeInterceptor</c> in the same transaction as the originating save. Deliberately does
/// NOT inherit <see cref="AuditableEntity"/>: it is never updated or soft-deleted, carries its own
/// <see cref="ChangedById"/>/<see cref="ChangedOnUtc"/>, and is the immutable source of field history.
/// </summary>
public class FieldModifiedLog
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The kind of entity whose field changed.</summary>
    public EntityType EntityType { get; set; }

    /// <summary>The id of the entity whose field changed.</summary>
    public Guid EntityId { get; set; }

    /// <summary>The tracked field key (e.g. <c>CreditLimit</c>).</summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>Human-readable previous value (enum→label, bool→Yes/No, …); null when previously unset.</summary>
    public string? OldValue { get; set; }

    /// <summary>Human-readable new value; null when cleared.</summary>
    public string? NewValue { get; set; }

    /// <summary>The user who made the change; null for system changes.</summary>
    public Guid? ChangedById { get; set; }

    /// <summary>When the change occurred (UTC).</summary>
    public DateTime ChangedOnUtc { get; set; }
}

/// <summary>
/// A tenant override that enables/disables an <em>optional</em> tracked field. System Tracked fields
/// have no row here (they are always enabled); a missing row for an optional field means enabled.
/// </summary>
public class ModifiedLogFieldConfig : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The entity type the tracked field belongs to.</summary>
    public EntityType EntityType { get; set; }

    /// <summary>The tracked field key.</summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>Whether the optional field is enabled for this tenant.</summary>
    public bool IsEnabled { get; set; } = true;
}
