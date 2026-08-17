namespace EmsPortal.Domain.Entities;

/// <summary>
/// One person's authority to work REMS requests on another's behalf — a shareholder or CSE naming a
/// delegate, modelled on Concur.
/// <para>
/// Rights are granular rather than one blanket "act as me", because the two halves are genuinely
/// different decisions: preparing a request commits nothing, while sending it puts the firm in front of a
/// client. Concur splits Can Prepare from Can Submit for exactly that reason, and so does this.
/// </para>
/// <para>
/// Deliberately does NOT extend to approving. Deciding an approval task is authorised by OWNING the task,
/// and a delegate holding both their own approver seat and their principal's could reach the decline
/// threshold single-handedly — or supply two of the approvals that carry a round. If approval delegation
/// is ever added, the rule that a round must never count two tasks decided by the same human has to come
/// with it.
/// </para>
/// </summary>
public class REMSDelegation : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The shareholder or CSE whose work is being delegated.</summary>
    public Guid PrincipalUserId { get; set; }

    /// <summary>The person acting on their behalf.</summary>
    public Guid DelegateUserId { get; set; }

    /// <summary>May create and fill requests as the principal.</summary>
    public bool CanPrepare { get; set; } = true;

    /// <summary>
    /// May email the intake link to the client. Off by default: without it the principal countersigns
    /// before anything leaves the building, which is the point of splitting the two.
    /// </summary>
    public bool CanSend { get; set; }

    /// <summary>First day the delegation applies (inclusive). Null = active from now.</summary>
    public DateOnly? StartsOn { get; set; }

    /// <summary>Last day the delegation applies (inclusive). Null = open-ended.</summary>
    public DateOnly? EndsOn { get; set; }

    // ---- Navigations ----
    public User? Principal { get; set; }
    public User? Delegate { get; set; }

    /// <summary>Whether the delegation is in force on <paramref name="on"/> (a tenant-local date).</summary>
    public bool IsActiveOn(DateOnly on)
        => (StartsOn is null || on >= StartsOn) && (EndsOn is null || on <= EndsOn);
}
