namespace IntegrationHub.Api.Models.Profile;

/// <summary>Writable address payload (upserted onto the person's primary address).</summary>
public sealed class AddressInput
{
    public string? AddressType { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? Landmark { get; set; }
    public string? Area { get; set; }
    public string? BuildingName { get; set; }
    public string? FloorNumber { get; set; }
    public string? UnitNumber { get; set; }
    public string? CountryCode { get; set; }
    public string? CountryName { get; set; }
    public string? StateCode { get; set; }
    public string? StateName { get; set; }
    public string? CityCode { get; set; }
    public string? CityName { get; set; }
    public string? PostalCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

/// <summary>Update payload for a person profile. Null fields are left unchanged.</summary>
public sealed class UpdatePersonProfileRequest
{
    // Personal
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? DisplayName { get; set; }
    public string? PreferredName { get; set; }
    public string? Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? MaritalStatus { get; set; }
    public string? Nationality { get; set; }
    public string? TimeZone { get; set; }
    public string? Language { get; set; }

    // Contact
    public string? PrimaryEmail { get; set; }
    public string? SecondaryEmail { get; set; }
    public string? MobileNumber { get; set; }
    public string? CountryCode { get; set; }
    public string? AlternateMobileNumber { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactRelationship { get; set; }
    public string? EmergencyContactNumber { get; set; }

    // Professional
    public string? EmployeeCode { get; set; }
    public string? JobTitle { get; set; }
    public string? Department { get; set; }
    public string? Organization { get; set; }

    // Social
    public string? LinkedInUrl { get; set; }
    public string? TwitterUrl { get; set; }
    public string? FacebookUrl { get; set; }
    public string? InstagramUrl { get; set; }
    public string? WebsiteUrl { get; set; }

    public string? Notes { get; set; }

    /// <summary>Profile image reference (a <c>Media</c> id). Set to attach/replace the photo.</summary>
    public Guid? ProfileMediaId { get; set; }

    /// <summary>When true, clears the profile image (takes precedence over <see cref="ProfileMediaId"/>).</summary>
    public bool RemoveProfileMedia { get; set; }

    /// <summary>Primary address; upserted when present.</summary>
    public AddressInput? Address { get; set; }
}

public sealed record AddressResponse(
    Guid Id,
    string AddressType,
    string? AddressLine1,
    string? AddressLine2,
    string? Landmark,
    string? Area,
    string? BuildingName,
    string? FloorNumber,
    string? UnitNumber,
    string? CountryCode,
    string? CountryName,
    string? StateCode,
    string? StateName,
    string? CityCode,
    string? CityName,
    string? PostalCode,
    double? Latitude,
    double? Longitude,
    bool IsValidated,
    string? ValidationSource);

public sealed record PersonProfileResponse(
    Guid Id,
    string PersonCode,
    Guid? UserId,
    Guid? TenantId,
    string FirstName,
    string? MiddleName,
    string LastName,
    string DisplayName,
    string FullName,
    string? PreferredName,
    string? Gender,
    DateTime? DateOfBirth,
    string? MaritalStatus,
    string? Nationality,
    string? TimeZone,
    string? Language,
    string? PrimaryEmail,
    string? SecondaryEmail,
    string? MobileNumber,
    string? CountryCode,
    string? AlternateMobileNumber,
    string? EmergencyContactName,
    string? EmergencyContactRelationship,
    string? EmergencyContactNumber,
    string? EmployeeCode,
    string? JobTitle,
    string? Department,
    string? Organization,
    string? LinkedInUrl,
    string? TwitterUrl,
    string? FacebookUrl,
    string? InstagramUrl,
    string? WebsiteUrl,
    int ProfileCompletionPercentage,
    bool IsProfileVerified,
    DateTime? LastProfileUpdatedOn,
    string? Notes,
    Guid? ProfileMediaId,
    string? ProfileMediaUrl,
    AddressResponse? Address);
