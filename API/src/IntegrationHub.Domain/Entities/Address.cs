using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Domain.Entities;

/// <summary>
/// Reusable address record shared across CRM modules (user/employee/company/billing/
/// shipping/office/vendor/customer addresses). Referenced by other entities via its
/// <see cref="Id"/> (WO-61).
/// </summary>
public class Address : AuditableEntity
{
    public Guid Id { get; set; }

    // ---- Address information ----
    public AddressType AddressType { get; set; } = AddressType.Home;
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? Landmark { get; set; }
    public string? Area { get; set; }
    public string? BuildingName { get; set; }
    public string? FloorNumber { get; set; }
    public string? UnitNumber { get; set; }

    // ---- Location information ----
    public string? CountryCode { get; set; }
    public string? CountryName { get; set; }
    public string? StateCode { get; set; }
    public string? StateName { get; set; }
    public string? CityCode { get; set; }
    public string? CityName { get; set; }
    public string? PostalCode { get; set; }

    // ---- Geolocation ----
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // ---- Validation ----
    public bool IsValidated { get; set; }
    public string? ValidationSource { get; set; }
}
