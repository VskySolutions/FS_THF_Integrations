using global::Hangfire;
using IntegrationHub.Application.Abstractions.Customers;

namespace IntegrationHub.Infrastructure.Jobs;

/// <summary>
/// Hangfire-backed <see cref="ICustomerSyncDispatcher"/>. Enqueues a one-off
/// <see cref="CustomerSyncJob"/> carrying the Customer Request and its owning tenant.
/// </summary>
internal sealed class CustomerSyncDispatcher : ICustomerSyncDispatcher
{
    private readonly IBackgroundJobClient _backgroundJobs;

    public CustomerSyncDispatcher(IBackgroundJobClient backgroundJobs)
    {
        _backgroundJobs = backgroundJobs;
    }

    public void Enqueue(Guid customerRequestId, Guid tenantId)
        => _backgroundJobs.Enqueue<CustomerSyncJob>(job => job.RunAsync(customerRequestId, tenantId, CancellationToken.None));
}
