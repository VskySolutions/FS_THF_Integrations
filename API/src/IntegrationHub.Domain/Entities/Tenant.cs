using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Domain.Entities;

/// <summary>
/// An independent organization served by the platform. Owns isolated integration data,
/// its own encrypted external-system credentials, and its own users (Multi-Tenancy).
/// </summary>
public class Tenant : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Human-readable tenant name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Unique, URL-safe slug. Immutable after creation.</summary>
    public string Identifier { get; set; } = string.Empty;

    /// <summary>IANA time zone id used to render this tenant's UTC timestamps. Defaults to UTC.</summary>
    public string TimeZoneId { get; set; } = "UTC";

    /// <summary>Lifecycle status. Defaults to Active on creation (AC-TNT-001.4).</summary>
    public TenantStatus Status { get; set; } = TenantStatus.Active;

    /// <summary>UTC timestamp when the tenant was created.</summary>
    public DateTime CreatedDate { get; set; }

    /// <summary>Per-tenant external system credential configurations.</summary>
    public ICollection<TenantApiConfiguration> ApiConfigurations { get; set; } = new List<TenantApiConfiguration>();
}
