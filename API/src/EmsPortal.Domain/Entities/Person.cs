using System.ComponentModel.DataAnnotations.Schema;
using EmsPortal.Domain.Enums;

namespace EmsPortal.Domain.Entities;

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

    // ---- Provenance ----
    // Persons arrive from several places, not just the Person screen: a REMS engagement's role contacts
    // and a client's submitted EMS form both mint one to satisfy REMSEntityContact.PersonId. Once mixed
    // into the same list they are indistinguishable, which matters — a contact captured off a public
    // form is not a colleague somebody onboarded, and the client picker offers all of them alike.
    //
    // Uses the platform's standard (EntityType, EntityId) pair (Universal Features ADR-001) rather than
    // an FK: the source is polymorphic, and a source record being deleted must not cascade into the
    // person it created.

    /// <summary>
    /// What kind of record created this person, e.g. <c>Rems</c> for a contact captured during an
    /// engagement or from a client's EMS form, <c>Person</c> for one entered on the Person screen.
    /// Null on rows written before provenance was tracked — unknown, not "created by nothing".
    /// <para>
    /// <c>Client</c> is the one value that says what the person IS rather than where they came from: a
    /// client of the firm, captured at REMS intake. It is what the client picker filters on, so a
    /// colleague or a role contact is never offered as somebody to open an engagement for.
    /// </para>
    /// </summary>
    public EntityType? SourceEntityType { get; set; }

    /// <summary>
    /// The specific record that created this person (the REMS request id, and so on). Null when the
    /// source is the Person screen itself, which has no record to point back at.
    /// </summary>
    public Guid? SourceEntityId { get; set; }

    // ---- Navigations ----
    public Address? Address { get; set; }
    public Media? ProfileMedia { get; set; }
    public Tenant? Tenant { get; set; }
}
