using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;

namespace EmsPortal.Application.Abstractions.Persistence;

/// <summary>
/// Data access for <see cref="CustomerRequest"/> records. Tenant isolation is applied by the
/// DbContext global query filter; admin/cross-tenant reads pass an explicit tenant id and bypass it.
/// </summary>
public interface ICustomerRequestRepository
{
    /// <summary>Full detail load including audit entries and documents.</summary>
    Task<CustomerRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Detail load bypassing the ambient tenant filter (background sync, Super Admin override).</summary>
    Task<CustomerRequest?> GetByIdForTenantAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Detail load bypassing the ambient tenant filter regardless of tenant (Super Admin cross-tenant).</summary>
    Task<CustomerRequest?> GetByIdUnscopedAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<CustomerRequest> Items, int Total)> ListAsync(
        string? search,
        Guid? tenantId,
        CustomerRequestStatus? status,
        Guid? submittedById,
        DateTime? fromUtc,
        DateTime? toUtc,
        Guid? draftViewerId,
        int page,
        int limit,
        CancellationToken cancellationToken = default,
        IReadOnlyCollection<Guid>? pinnedFirstIds = null);

    /// <summary>
    /// Step 1 duplicate candidates within a tenant: matches Company Name, Legal Name, or Email Address.
    /// Excludes the supplied record id (so resubmits don't match themselves).
    /// </summary>
    Task<IReadOnlyList<CustomerRequest>> FindStep1DuplicatesAsync(
        Guid tenantId, Guid excludeId, string companyName, string legalName, string emailAddress, CancellationToken cancellationToken = default);

    /// <summary>Step 2 duplicate candidates within a tenant: matches Tax Number. Excludes the supplied id.</summary>
    Task<IReadOnlyList<CustomerRequest>> FindTaxNumberDuplicatesAsync(
        Guid tenantId, Guid excludeId, string taxNumber, CancellationToken cancellationToken = default);

    /// <summary>Count of requests for the tenant whose number was issued in the given year (for number generation).</summary>
    Task<int> CountForYearAsync(Guid tenantId, int year, CancellationToken cancellationToken = default);

    Task AddAsync(CustomerRequest request, CancellationToken cancellationToken = default);

    void Update(CustomerRequest request);

    void Remove(CustomerRequest request);
}
