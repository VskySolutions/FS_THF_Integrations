namespace EmsPortal.Domain.Enums;

/// <summary>
/// The kind of workflow action captured by an immutable <see cref="Entities.CustomerAuditEntry"/>.
/// </summary>
public enum CustomerAuditActionType
{
    Created = 0,
    Submitted = 1,
    Enriched = 2,
    SentForApproval = 3,
    Step2Saved = 4,
    Approved = 5,
    Rejected = 6,
    Returned = 7,
    Reopened = 8,
    DuplicateAcknowledged = 13,
    DocumentUploaded = 14,
    DocumentRemoved = 15,
    RevertedToReviewer = 16
}
