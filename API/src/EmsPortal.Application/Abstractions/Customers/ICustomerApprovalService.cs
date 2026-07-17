using EmsPortal.Domain.Entities;

namespace EmsPortal.Application.Abstractions.Customers;

/// <summary>
/// Thrown when a workflow transition is invalid (e.g. approving with incomplete Step 2 fields, or
/// acting on a request in the wrong status). Surfaced by the controller as a 4xx error envelope.
/// </summary>
public sealed class CustomerWorkflowException : Exception
{
    public CustomerWorkflowException(string message, string? details = null) : base(message)
        => Details = details ?? message;

    /// <summary>Human-readable detail (e.g. the list of missing mandatory fields).</summary>
    public string Details { get; }
}

/// <summary>
/// Owns the multi-stage approval state machine for a <see cref="CustomerRequest"/>. Validates the
/// mandatory Step 2 fields before an approval, advances or finalises the approval stage, records
/// audit entries, and persists. Final approval is the terminal success state.
/// </summary>
public interface ICustomerApprovalService
{
    /// <summary>Mandatory Step 2 field names that are not yet populated on the request (empty = complete).</summary>
    IReadOnlyList<string> GetMissingMandatoryStep2Fields(CustomerRequest request);

    /// <summary>
    /// Approves the current stage. Validates Step 2 completeness (throws
    /// <see cref="CustomerWorkflowException"/> if incomplete), advances the stage, and on the final
    /// stage sets the request to Sync In Progress and enqueues the sync job. Persists in one transaction.
    /// </summary>
    Task ApproveAsync(CustomerRequest request, Guid? actorId, string? actorName, CancellationToken cancellationToken = default);

    /// <summary>Rejects the request with a mandatory reason. Persists and audits.</summary>
    Task RejectAsync(CustomerRequest request, string reason, Guid? actorId, string? actorName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an awaiting-approval request back to the reviewer (status → Under Review), resetting the
    /// approval stage and recording optional notes. Persists and audits.
    /// </summary>
    Task RevertToReviewerAsync(CustomerRequest request, string? notes, Guid? actorId, string? actorName, CancellationToken cancellationToken = default);

    /// <summary>Returns the request for corrections, unlocking the identified fields. Persists and audits.</summary>
    Task ReturnAsync(CustomerRequest request, string notes, IReadOnlyList<string> fields, Guid? actorId, string? actorName, CancellationToken cancellationToken = default);
}
