using IntegrationHub.Domain.Entities;

namespace IntegrationHub.Api.Models.Profile;

/// <summary>Shared projection of a <see cref="Person"/> (and its address) to API response shapes.</summary>
public static class PersonProfileMapper
{
    public static PersonProfileResponse Map(Person p) => new(
        p.Id, p.PersonCode, p.UserId, p.TenantId,
        p.FirstName, p.MiddleName, p.LastName, p.DisplayName, p.FullName,
        p.PreferredName, p.Gender, p.DateOfBirth, p.MaritalStatus, p.Nationality, p.TimeZone, p.Language,
        p.PrimaryEmail, p.SecondaryEmail, p.MobileNumber, p.CountryCode, p.AlternateMobileNumber,
        p.EmergencyContactName, p.EmergencyContactRelationship, p.EmergencyContactNumber,
        p.EmployeeCode, p.JobTitle, p.Department, p.Organization,
        p.LinkedInUrl, p.TwitterUrl, p.FacebookUrl, p.InstagramUrl, p.WebsiteUrl,
        p.ProfileCompletionPercentage, p.IsProfileVerified, p.LastProfileUpdatedOn, p.Notes,
        p.ProfileMediaId, p.ProfileMedia?.PublicUrl,
        p.Address is null ? null : MapAddress(p.Address));

    public static AddressResponse MapAddress(Address a) => new(
        a.Id, a.AddressType.ToString(), a.AddressLine1, a.AddressLine2, a.Landmark, a.Area,
        a.BuildingName, a.FloorNumber, a.UnitNumber, a.CountryCode, a.CountryName, a.StateCode,
        a.StateName, a.CityCode, a.CityName, a.PostalCode, a.Latitude, a.Longitude,
        a.IsValidated, a.ValidationSource);
}
