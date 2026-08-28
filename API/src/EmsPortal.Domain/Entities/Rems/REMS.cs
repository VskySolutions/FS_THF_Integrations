using System.ComponentModel.DataAnnotations.Schema;

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

    /// <summary>
    /// The initiator's message ("Message from Partner"). No longer asked for — the request's Conversation
    /// thread carries that context, because it reaches the admin, the CSE and the approvers and can be
    /// replied to. Kept because older requests hold text somebody wrote, and the request page still shows
    /// it where there is any.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// How this referral relates to THF: a foreign key to the <c>REMS.Type</c> option-set item. Required —
    /// a request is always one or the other, and the client lookup marks it by the code this resolves to.
    /// </summary>
    public Guid TypeId { get; set; }

    /// <summary>
    /// The stage the request is at: a foreign key to the <c>REMS.Status</c> option-set item. Required.
    ///
    /// <para>
    /// The whole workflow keys off the CODE this resolves to (RemsRequestStatuses), which is why the
    /// values on that list cannot be added to, deleted or re-coded. Read it through the
    /// <see cref="Status"/> navigation.
    /// </para>
    /// </summary>
    public Guid StatusId { get; set; }

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

    /// <summary>Name of the client as requested at intake, WITHOUT the generational suffix.</summary>
    public string RequestedClientName { get; set; } = string.Empty;

    /// <summary>
    /// The generational suffix on the client's name — Jr., Sr., II, III, IV — kept apart from the name
    /// itself. Free text with those five offered as suggestions: the list is what most clients need, not
    /// what any client may have, and a suffix nobody thought to seed is not a reason to file a client
    /// under the wrong name.
    /// <para>
    /// Held separately rather than typed into the name box so that the two are separable afterwards: a
    /// person record splits into first and last name, and "Jr." belongs to neither. <see
    /// cref="ClientDisplayName"/> is what puts them back together for reading.
    /// </para>
    /// </summary>
    public string? ClientNameSuffix { get; set; }

    /// <summary>
    /// The client's name as it reads — the requested name with the suffix appended. This is what every
    /// list, notification and email shows; <see cref="RequestedClientName"/> on its own would drop the
    /// suffix silently wherever it was used.
    /// </summary>
    [NotMapped]
    public string ClientDisplayName => Append(RequestedClientName?.Trim() ?? string.Empty);

    /// <summary>
    /// Any name of this client, read as it should be — with the request's suffix on the end.
    /// <para>
    /// Needed because the client's name exists in two places once their intake form comes back: the name
    /// the request was raised under (<see cref="RequestedClientName"/>) and the name the CLIENT typed,
    /// which is what <c>REMSClient.Name</c> and the main <c>REMSEntity.Name</c> hold. The intake form
    /// never asks for a suffix — it is the firm's own particle on the name, set at intake — so a surface
    /// showing the client's own version was showing "John Smith" where every list beside it said
    /// "John Smith Jr.".
    /// </para>
    /// <para>
    /// A blank name falls back to <see cref="ClientDisplayName"/>; a name that already carries the suffix
    /// is returned untouched, so this is safe to apply to a value that may already have been through it.
    /// </para>
    /// </summary>
    public string WithClientSuffix(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        // Via Append rather than ClientDisplayName so the blank case cannot recurse — ClientDisplayName is
        // itself Append(RequestedClientName), and RequestedClientName can be empty on an unsaved request.
        return Append(trimmed.Length == 0 ? RequestedClientName?.Trim() ?? string.Empty : trimmed);
    }

    /// <summary>The suffix appended once — a name that already ends with it is left alone.</summary>
    private string Append(string name)
    {
        var suffix = ClientNameSuffix?.Trim() ?? string.Empty;
        if (name.Length == 0 || suffix.Length == 0 || name.EndsWith(" " + suffix, StringComparison.OrdinalIgnoreCase))
        {
            return name;
        }

        return $"{name} {suffix}";
    }

    /// <summary>Customer email used to reach out; required together-or-with mobile at app level.</summary>
    public string? CustomerEmail { get; set; }

    /// <summary>Customer mobile number; required together-or-with email at app level.</summary>
    public string? CustomerMobileNumber { get; set; }

    /// <summary>
    /// The shareholder or CSE this request was raised FOR, when a delegate raised it on their behalf.
    /// Null when the creator was acting as themselves.
    /// <para>
    /// Dual attribution: <c>CreatedById</c> keeps the person who actually did it, and this keeps whose
    /// work it is — "prepared by X on behalf of Y". It is also what puts the request in the principal's
    /// list rather than only the delegate's.
    /// </para>
    /// </summary>
    public Guid? OnBehalfOfUserId { get; set; }

    // ---- Navigations ----
    public OptionSetItem? Type { get; set; }
    public OptionSetItem? Status { get; set; }
    public Person? ClientPerson { get; set; }
    public ICollection<REMSFiles> Files { get; set; } = new List<REMSFiles>();
    public ICollection<REMSForm> Forms { get; set; } = new List<REMSForm>();
    public ICollection<REMSClient> Clients { get; set; } = new List<REMSClient>();

    /// <summary>Other businesses the client declared at intake, each awaiting its own request.</summary>
    public ICollection<REMSAdditionalEntity> AdditionalEntities { get; set; } = new List<REMSAdditionalEntity>();

    /// <summary>Every time the admin returned this request to its initiator, newest last.</summary>
    public ICollection<REMSSendBack> SendBacks { get; set; } = new List<REMSSendBack>();
}
