using EmsPortal.Api.Models.Profile;
using EmsPortal.Api.Security;
using EmsPortal.Application.Abstractions.Auditing;
using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;
using EmsPortal.Shared.Contracts;
using EmsPortal.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmsPortal.Api.Controllers;

/// <summary>
/// Person profile management (WO-61): the rich personal/contact/professional/social/address
/// profile backed by the <see cref="Person"/> master record. Users manage their own profile;
/// admins (users.read / users.write) manage any user's profile.
/// </summary>
[ApiController]
[Produces("application/json")]
[Tags("Profile")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
public sealed class ProfileController : ControllerBase
{
    private readonly IPersonRepository _persons;
    private readonly IAddressRepository _addresses;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditTrailService _audit;

    public ProfileController(
        IPersonRepository persons,
        IAddressRepository addresses,
        IUnitOfWork unitOfWork,
        IAuditTrailService audit)
    {
        _persons = persons;
        _addresses = addresses;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    [HttpGet("/api/users/me/profile")]
    [Authorize]
    [ProducesResponseType<ApiResponse<PersonProfileResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        var person = await _persons.GetByUserIdAsync(userId.Value, cancellationToken);
        return person is null
            ? NotFound(ApiResponseFactory.NotFound("Profile not found."))
            : Ok(ApiResponseFactory.Success(PersonProfileMapper.Map(person), "Profile retrieved."));
    }

    [HttpPut("/api/users/me/profile")]
    [Authorize]
    [ProducesResponseType<ApiResponse<PersonProfileResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdatePersonProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        var person = await _persons.GetByUserIdAsync(userId.Value, cancellationToken);
        if (person is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Profile not found."));
        }

        return await ApplyUpdateAsync(person, request, cancellationToken);
    }

    [HttpGet("/api/admin/users/{userId:guid}/profile")]
    [RequirePermission(Permissions.UsersRead)]
    [ProducesResponseType<ApiResponse<PersonProfileResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserProfile(Guid userId, CancellationToken cancellationToken)
    {
        var person = await _persons.GetByUserIdAsync(userId, cancellationToken);
        return person is null
            ? NotFound(ApiResponseFactory.NotFound("Profile not found."))
            : Ok(ApiResponseFactory.Success(PersonProfileMapper.Map(person), "Profile retrieved."));
    }

    [HttpPut("/api/admin/users/{userId:guid}/profile")]
    [RequirePermission(Permissions.UsersWrite)]
    [ProducesResponseType<ApiResponse<PersonProfileResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateUserProfile(Guid userId, [FromBody] UpdatePersonProfileRequest request, CancellationToken cancellationToken)
    {
        var person = await _persons.GetByUserIdAsync(userId, cancellationToken);
        if (person is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Profile not found."));
        }

        return await ApplyUpdateAsync(person, request, cancellationToken);
    }

    private async Task<IActionResult> ApplyUpdateAsync(Person person, UpdatePersonProfileRequest request, CancellationToken cancellationToken)

    {
        // Personal
        Apply(request.FirstName, v => person.FirstName = v);
        Apply(request.MiddleName, v => person.MiddleName = v);
        Apply(request.LastName, v => person.LastName = v);
        Apply(request.PreferredName, v => person.PreferredName = v);
        Apply(request.Gender, v => person.Gender = v);
        Apply(request.MaritalStatus, v => person.MaritalStatus = v);
        Apply(request.Nationality, v => person.Nationality = v);
        Apply(request.TimeZone, v => person.TimeZone = v);
        Apply(request.Language, v => person.Language = v);
        if (request.DateOfBirth.HasValue)
        {
            person.DateOfBirth = request.DateOfBirth;
        }
        if (request.DisplayName is { } displayName)
        {
            person.DisplayName = displayName;
        }

        // Contact
        Apply(request.PrimaryEmail, v => person.PrimaryEmail = v);
        Apply(request.SecondaryEmail, v => person.SecondaryEmail = v);
        Apply(request.MobileNumber, v => person.MobileNumber = v);
        Apply(request.CountryCode, v => person.CountryCode = v);
        Apply(request.AlternateMobileNumber, v => person.AlternateMobileNumber = v);
        Apply(request.EmergencyContactName, v => person.EmergencyContactName = v);
        Apply(request.EmergencyContactRelationship, v => person.EmergencyContactRelationship = v);
        Apply(request.EmergencyContactNumber, v => person.EmergencyContactNumber = v);

        // Professional
        Apply(request.EmployeeCode, v => person.EmployeeCode = v);
        Apply(request.Department, v => person.Department = v);
        Apply(request.Organization, v => person.Organization = v);

        // Social
        Apply(request.LinkedInUrl, v => person.LinkedInUrl = v);
        Apply(request.TwitterUrl, v => person.TwitterUrl = v);
        Apply(request.FacebookUrl, v => person.FacebookUrl = v);
        Apply(request.InstagramUrl, v => person.InstagramUrl = v);
        Apply(request.WebsiteUrl, v => person.WebsiteUrl = v);
        Apply(request.Notes, v => person.Notes = v);

        // Profile image
        if (request.RemoveProfileMedia)
        {
            person.ProfileMediaId = null;
        }
        else if (request.ProfileMediaId.HasValue)
        {
            person.ProfileMediaId = request.ProfileMediaId;
        }

        // Address (upsert onto the person's primary address)
        if (request.Address is { } addressInput)
        {
            await UpsertAddressAsync(person, addressInput, cancellationToken);
        }

        person.LastProfileUpdatedOn = DateTime.UtcNow;
        person.ProfileCompletionPercentage = ComputeCompletion(person);
        _persons.Update(person);

        await _audit.AddAsync(nameof(Person), person.Id.ToString(), "ProfileUpdated", cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Re-load to project the latest address/media navigations.
        var refreshed = await _persons.GetByIdAsync(person.Id, cancellationToken) ?? person;
        return Ok(ApiResponseFactory.Success(PersonProfileMapper.Map(refreshed), "Profile updated."));
    }

    private async Task UpsertAddressAsync(Person person, AddressInput input, CancellationToken cancellationToken)
    {
        var address = person.AddressId is { } addressId
            ? await _addresses.GetByIdAsync(addressId, cancellationToken)
            : null;

        var isNew = address is null;
        address ??= new Address { Id = Guid.NewGuid() };

        address.AddressType = Enum.TryParse<AddressType>(input.AddressType, ignoreCase: true, out var type) ? type : AddressType.Home;
        address.AddressLine1 = input.AddressLine1;
        address.AddressLine2 = input.AddressLine2;
        address.Landmark = input.Landmark;
        address.Area = input.Area;
        address.BuildingName = input.BuildingName;
        address.FloorNumber = input.FloorNumber;
        address.UnitNumber = input.UnitNumber;
        address.CountryCode = input.CountryCode;
        address.CountryName = input.CountryName;
        address.StateCode = input.StateCode;
        address.StateName = input.StateName;
        address.CityCode = input.CityCode;
        address.CityName = input.CityName;
        address.PostalCode = input.PostalCode;
        address.Latitude = input.Latitude;
        address.Longitude = input.Longitude;

        if (isNew)
        {
            await _addresses.AddAsync(address, cancellationToken);
            person.AddressId = address.Id;
        }
        else
        {
            _addresses.Update(address);
        }
    }

    private static void Apply(string? value, Action<string> set)
    {
        if (value is not null)
        {
            set(value);
        }
    }

    /// <summary>Rough completion score over a representative set of core profile fields.</summary>
    private static int ComputeCompletion(Person p)
    {
        var fields = new[]
        {
            p.FirstName, p.LastName, p.DisplayName, p.PreferredName, p.Gender,
            p.PrimaryEmail, p.MobileNumber, p.Nationality, p.Organization
        };
        var filled = fields.Count(f => !string.IsNullOrWhiteSpace(f));
        var total = fields.Length + 2; // + date of birth + address
        if (p.DateOfBirth.HasValue) filled++;
        if (p.AddressId.HasValue) filled++;
        return (int)Math.Round(filled * 100.0 / total);
    }
}
