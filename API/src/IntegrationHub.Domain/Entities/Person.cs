using System.ComponentModel.DataAnnotations.Schema;

namespace IntegrationHub.Domain.Entities;

/// <summary>
/// Canonical CRM person master record holding all personal profile information for a
/// <see cref="User"/> (and, in future, CRM contacts/employees/customers). Authentication
/// data stays on <see cref="User"/>; profile data lives here (WO-61).
/// </summary>
public class Person : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>Unique business identifier (e.g. PER-000123).</summary>
    public string PersonCode { get; set; } = string.Empty;

    /// <summary>Back-reference to the owning user (the FK lives on <see cref="User.PersonId"/>).</summary>
    public Guid? UserId { get; set; }

    /// <summary>Owning tenant for this person (optional; platform-level persons have none).</summary>
    public Guid? TenantId { get; set; }

    public Guid? ProfileMediaId { get; set; }

    public Guid? AddressId { get; set; }

    // ---- Personal information ----
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? PreferredName { get; set; }
    public string? Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? MaritalStatus { get; set; }
    public string? Nationality { get; set; }
    public string? TimeZone { get; set; }
    public string? Language { get; set; }

    /// <summary>FirstName + MiddleName + LastName (trimmed); falls back to DisplayName.</summary>
    [NotMapped]
    public string FullName =>
        string.Join(" ", new[] { FirstName, MiddleName, LastName }.Where(s => !string.IsNullOrWhiteSpace(s))) is { Length: > 0 } name
            ? name
            : DisplayName;

    // ---- Contact information ----
    public string? PrimaryEmail { get; set; }
    public string? SecondaryEmail { get; set; }
    public string? MobileNumber { get; set; }
    public string? CountryCode { get; set; }
    public string? AlternateMobileNumber { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactRelationship { get; set; }
    public string? EmergencyContactNumber { get; set; }

    // ---- Professional information ----
    public string? EmployeeCode { get; set; }
    public string? JobTitle { get; set; }
    public string? Department { get; set; }
    public string? Organization { get; set; }
    public Guid? ManagerPersonId { get; set; }

    // ---- Social information ----
    public string? LinkedInUrl { get; set; }
    public string? TwitterUrl { get; set; }
    public string? FacebookUrl { get; set; }
    public string? InstagramUrl { get; set; }
    public string? WebsiteUrl { get; set; }

    // ---- Profile metadata ----
    public int ProfileCompletionPercentage { get; set; }
    public bool IsProfileVerified { get; set; }
    public DateTime? LastProfileUpdatedOn { get; set; }
    public string? Notes { get; set; }

    /// <summary>Active flag (soft-delete handled by <see cref="AuditableEntity.Deleted"/>).</summary>
    public bool IsActive { get; set; } = true;

    // ---- Navigations ----
    public Address? Address { get; set; }
    public Media? ProfileMedia { get; set; }
    public Tenant? Tenant { get; set; }
}
