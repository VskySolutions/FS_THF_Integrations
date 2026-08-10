namespace EmsPortal.Domain.Entities;

/// <summary>
/// Root of a REMS (Request for Engagement / new-client onboarding) request (WO-110). Tenant-owned and
/// soft-deletable. Carries the staff-facing intake details; the customer-facing form, the resulting
/// client, and downstream engagements hang off this aggregate. Option-set-valued columns
/// (<see cref="Type"/>, <see cref="Status"/>) store the option item's string code, not a foreign key.
/// </summary>
public class REMS : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Human-readable request number, unique per tenant (e.g. <c>REMS-1</c>).</summary>
    public string REMSNumber { get; set; } = string.Empty;

    /// <summary>Short request title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional free-text description.</summary>
    public string? Description { get; set; }

    /// <summary>Request type (option-set <c>REMS.Type</c> code).</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Request status (option-set <c>REMS.Status</c> code).</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Admin/staff member the request is assigned to (User).</summary>
    public Guid? AdminAssignedToId { get; set; }

    /// <summary>Client Service Executive assigned to the request (User).</summary>
    public Guid? CSEId { get; set; }

    /// <summary>Loose reference to an existing client record (not a foreign key).</summary>
    public Guid? ExistingClientReferenceId { get; set; }

    /// <summary>
    /// The <see cref="Person"/> master record this request's client is, set on every save. Where
    /// <see cref="ExistingClientReferenceId"/> records that intake <em>matched</em> a client THF already
    /// had — null for a brand-new client — this is simply who the client is, minted on the spot when
    /// nobody matched. That is what makes the client findable in the picker next time, and what a later
    /// "convert this client into a user" hangs off (a User points at a Person via <c>User.PersonId</c>).
    /// <para>
    /// Null only on requests written before the column existed and never saved since.
    /// </para>
    /// </summary>
    public Guid? ClientPersonId { get; set; }

    /// <summary>Name of the client as requested at intake.</summary>
    public string RequestedClientName { get; set; } = string.Empty;

    /// <summary>Customer email used to reach out; required together-or-with mobile at app level.</summary>
    public string? CustomerEmail { get; set; }

    /// <summary>Customer mobile number; required together-or-with email at app level.</summary>
    public string? CustomerMobileNumber { get; set; }

    // ---- Navigations ----
    public Person? ClientPerson { get; set; }
    public ICollection<REMSFiles> Files { get; set; } = new List<REMSFiles>();
    public ICollection<REMSForm> Forms { get; set; } = new List<REMSForm>();
    public ICollection<REMSClient> Clients { get; set; } = new List<REMSClient>();
}
