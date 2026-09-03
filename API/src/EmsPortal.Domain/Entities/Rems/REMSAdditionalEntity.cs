namespace EmsPortal.Domain.Entities;

/// <summary>
/// Another business the client told us about on their intake form ("Do you have more entities?"), held
/// as a contact rather than as a <see cref="REMSEntity"/>.
/// <para>
/// These do NOT fan out into engagements. A request carries exactly one <see cref="REMSEngagement"/>, so
/// each additional entity becomes its own REMS request instead — raised by hand from the Partner/CSE
/// list, which is what <see cref="CreatedREMSId"/> records. Until that happens the row is unhandled and
/// the originating request stays flagged; once it is set the flag clears for that row and links to the
/// request it produced. Without it the flag would sit on the request forever, and the Partner and the CSE
/// — who both see the same list — would each raise an EMS for the same entity.
/// </para>
/// </summary>
public class REMSAdditionalEntity : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The request whose intake declared this entity.</summary>
    public Guid REMSId { get; set; }

    /// <summary>Stable key identifying this row within the submitted payload.</summary>
    public string SourceKey { get; set; } = string.Empty;

    /// <summary>Contact's full name.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Contact's email address.</summary>
    public string? EmailAddress { get; set; }

    /// <summary>Contact's phone number.</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// The follow-up request raised from this row, or null while it is still outstanding. Deliberately a
    /// loose reference rather than a parent/child relationship: the new request stands on its own and is
    /// not presented as belonging to a family.
    /// </summary>
    public Guid? CreatedREMSId { get; set; }

    /// <summary>
    /// How far this related client has got, as a foreign key to the <c>REMS.RelatedEntityStatus</c>
    /// option-set item. Null until somebody sets it, which reads as
    /// <see cref="RemsRelatedEntityStatuses.NotInitiated"/> — see that class for why it is set by hand and
    /// never by the workflow.
    /// <para>
    /// Deliberately NOT derived from <see cref="CreatedREMSId"/> beside it, though the two are about the
    /// same thing. That column records one specific act — somebody pressed Create EMS from this row — and
    /// a firm that raises the follow-up request any other way, or that is chasing the client for documents
    /// before raising anything at all, has a position to report that the column cannot express.
    /// </para>
    /// </summary>
    public Guid? RelatedStatusId { get; set; }

    // ---- Navigations ----
    public REMS? Rems { get; set; }
    public OptionSetItem? RelatedStatus { get; set; }
}
