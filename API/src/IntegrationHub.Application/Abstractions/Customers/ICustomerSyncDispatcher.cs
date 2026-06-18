namespace IntegrationHub.Application.Abstractions.Customers;

/// <summary>
/// Enqueues the Maconomy customer-sync background job. Abstracted so the Application layer
/// (e.g. the approval state machine) can trigger a sync without referencing Hangfire directly.
/// </summary>
public interface ICustomerSyncDispatcher
{
    /// <summary>Enqueues a one-off sync for an approved Customer Request, carrying the owning tenant.</summary>
    void Enqueue(Guid customerRequestId, Guid tenantId);
}
