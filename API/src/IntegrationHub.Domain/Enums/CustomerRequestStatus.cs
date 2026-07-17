namespace IntegrationHub.Domain.Enums;

/// <summary>
/// Lifecycle status of a <see cref="Entities.CustomerRequest"/>. Transitions are one-way
/// except: Returned re-submission restarts the workflow at Submitted, and a Rejected record
/// may be reopened (set to Returned) by a Tenant/Super Admin.
/// </summary>
public enum CustomerRequestStatus
{
    /// <summary>Saved but not yet submitted; fully editable, no Customer Request Number.</summary>
    Draft = 0,

    /// <summary>Submitted with Step 1 complete; Step 1 fields locked; awaiting enrichment.</summary>
    Submitted = 1,

    /// <summary>A Customer Role User is enriching the record with internal business fields.</summary>
    UnderReview = 2,

    /// <summary>Sent for approval; awaiting a Customer Approver to complete Step 2 and approve.</summary>
    PendingApproval = 3,

    /// <summary>One or more (but not all) approval stages completed in a multi-stage flow.</summary>
    PartiallyApproved = 4,

    /// <summary>All approval stages completed; terminal success state of the workflow.</summary>
    Approved = 5,

    /// <summary>Rejected by an approver; only a Tenant/Super Admin may reopen it.</summary>
    Rejected = 8,

    /// <summary>Returned for corrections; identified fields are unlocked for editing.</summary>
    Returned = 9
}
