using EmsPortal.Domain.Enums;

namespace EmsPortal.Domain.Entities;

/// <summary>
/// An immutable, append-only audit record for a single workflow action taken on a
/// <see cref="CustomerRequest"/> (submit, enrich, approve, reject, return, retry, sync, …).
/// Tagged with the owning tenant, the acting user, a timestamp, and any notes or affected fields.
/// </summary>
public class CustomerAuditEntry : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>The Customer Request this entry belongs to.</summary>
    public Guid CustomerRequestId { get; set; }

    /// <summary>Owning tenant (matches the parent request; used for tenant-scoped queries).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The kind of action performed.</summary>
    public CustomerAuditActionType ActionType { get; set; }

    /// <summary>User who performed the action (null for system/background operations).</summary>
    public Guid? PerformedById { get; set; }

    /// <summary>Display name of the actor at the time of the action (denormalised for the timeline).</summary>
    public string? PerformedBy { get; set; }

    /// <summary>UTC timestamp the action was performed.</summary>
    public DateTime PerformedOnUtc { get; set; }

    /// <summary>Free-text notes/comments (e.g. rejection reason, correction notes), recorded verbatim.</summary>
    public string? Notes { get; set; }

    /// <summary>Optional JSON describing the fields affected by the action.</summary>
    public string? FieldsAffected { get; set; }

    public CustomerRequest? CustomerRequest { get; set; }
}
