using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Domain.Entities;

/// <summary>
/// Per-tenant external system API credentials. The credential blob is encrypted at rest
/// via the .NET Data Protection API (Multi-Tenancy ADR-002); plaintext is never stored.
/// </summary>
public class TenantApiConfiguration : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant.</summary>
    public Guid TenantId { get; set; }

    /// <summary>External system these credentials belong to (Concur or Maconomy).</summary>
    public SystemName System { get; set; }

    /// <summary>Encrypted credential blob (JSON, protected).</summary>
    public string EncryptedCredentials { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the configuration was created.</summary>
    public DateTime CreatedDate { get; set; }

    /// <summary>UTC timestamp when the configuration was last updated.</summary>
    public DateTime? UpdatedDate { get; set; }

    /// <summary>Navigation to the owning tenant.</summary>
    public Tenant? Tenant { get; set; }
}
