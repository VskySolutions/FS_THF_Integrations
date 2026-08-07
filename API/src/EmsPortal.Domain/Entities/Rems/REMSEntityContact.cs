namespace EmsPortal.Domain.Entities;

/// <summary>
/// A contact person on a <see cref="REMSEntity"/> (WO-110). At most one contact per
/// (entity, <see cref="ContactRole"/>). <see cref="ContactRole"/> stores a
/// <see cref="Enums.RemsContactRole"/> value as a string code. Links to a shared <see cref="Person"/>.
/// </summary>
public class REMSEntityContact : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Owning entity.</summary>
    public Guid REMSEntityId { get; set; }

    /// <summary>The shared person record.</summary>
    public Guid PersonId { get; set; }

    /// <summary>Contact role code (a <see cref="Enums.RemsContactRole"/> value).</summary>
    public string ContactRole { get; set; } = string.Empty;

    /// <summary>Whether this role is required for the entity's industry group.</summary>
    public bool IsRequired { get; set; }

    // ---- Navigations ----
    public REMSEntity? Entity { get; set; }
    public Person? Person { get; set; }
}
