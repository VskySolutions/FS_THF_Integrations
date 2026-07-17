using EmsPortal.Application.Abstractions.Customers;
using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Domain.Entities;

namespace EmsPortal.Application.Customers;

/// <summary>
/// Default <see cref="ICustomerDuplicateChecker"/>. Compares only within the supplied tenant
/// scope, checking the platform's own Customer Requests.
/// </summary>
public sealed class CustomerDuplicateChecker : ICustomerDuplicateChecker
{
    private readonly ICustomerRequestRepository _requests;

    public CustomerDuplicateChecker(ICustomerRequestRepository requests)
    {
        _requests = requests;
    }

    public async Task<IReadOnlyList<CustomerDuplicateMatch>> CheckStep1Async(
        Guid tenantId, CustomerRequest candidate, CancellationToken cancellationToken = default)
    {
        var matches = await _requests.FindStep1DuplicatesAsync(
            tenantId, candidate.Id, candidate.CompanyName, candidate.LegalName, candidate.EmailAddress, cancellationToken);

        return matches.Select(m =>
        {
            var fields = new List<string>();
            if (string.Equals(m.CompanyName, candidate.CompanyName, StringComparison.OrdinalIgnoreCase))
            {
                fields.Add("Company Name");
            }
            if (string.Equals(m.LegalName, candidate.LegalName, StringComparison.OrdinalIgnoreCase))
            {
                fields.Add("Legal Name");
            }
            if (string.Equals(m.EmailAddress, candidate.EmailAddress, StringComparison.OrdinalIgnoreCase))
            {
                fields.Add("Email Address");
            }
            return new CustomerDuplicateMatch(m.Id, m.CustomerRequestNumber, m.CompanyName, fields);
        }).ToList();
    }

    public async Task<IReadOnlyList<CustomerDuplicateMatch>> CheckTaxNumberAsync(
        Guid tenantId, Guid excludeId, string taxNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taxNumber))
        {
            return Array.Empty<CustomerDuplicateMatch>();
        }

        var matches = await _requests.FindTaxNumberDuplicatesAsync(tenantId, excludeId, taxNumber.Trim(), cancellationToken);
        return matches
            .Select(m => new CustomerDuplicateMatch(m.Id, m.CustomerRequestNumber, m.CompanyName, new[] { "Tax Number" }))
            .ToList();
    }
}
