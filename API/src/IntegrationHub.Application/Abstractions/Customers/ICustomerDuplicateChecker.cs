using IntegrationHub.Domain.Entities;

namespace IntegrationHub.Application.Abstractions.Customers;

/// <summary>A potential duplicate Customer Request surfaced by the duplicate checker.</summary>
public sealed record CustomerDuplicateMatch(
    Guid Id,
    string? CustomerRequestNumber,
    string CompanyName,
    IReadOnlyList<string> MatchedFields);

/// <summary>
/// Advisory (non-blocking) duplicate detection within a single tenant. Step 1 matches on
/// Company Name / Legal Name / Email Address at submission; Step 2 matches on Tax Number at
/// approval. Acknowledgement of any match is recorded by the caller in the audit trail.
/// </summary>
public interface ICustomerDuplicateChecker
{
    Task<IReadOnlyList<CustomerDuplicateMatch>> CheckStep1Async(
        Guid tenantId, CustomerRequest candidate, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerDuplicateMatch>> CheckTaxNumberAsync(
        Guid tenantId, Guid excludeId, string taxNumber, CancellationToken cancellationToken = default);
}
