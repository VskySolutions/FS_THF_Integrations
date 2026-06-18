using FluentAssertions;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Customers;
using IntegrationHub.Domain.Entities;
using Moq;

namespace IntegrationHub.UnitTests;

// WO-66: advisory duplicate detection (tenant-scoped).
public class CustomerDuplicateCheckerTests
{
    private readonly Mock<ICustomerRequestRepository> _requests = new();

    private CustomerDuplicateChecker Create() => new(_requests.Object);

    [Fact]
    public async Task CheckStep1_returns_matches_with_correct_matched_fields_and_is_tenant_scoped()
    {
        var tenantId = Guid.NewGuid();
        var candidate = new CustomerRequest
        {
            Id = Guid.NewGuid(),
            CompanyName = "Acme",
            LegalName = "Acme Inc",
            EmailAddress = "billing@acme.com",
        };

        // Match 1: same company name + email (different legal name).
        // Match 2: same legal name only.
        var matches = new List<CustomerRequest>
        {
            new() { Id = Guid.NewGuid(), CustomerRequestNumber = "CUS-2026-000001", CompanyName = "Acme", LegalName = "Different", EmailAddress = "BILLING@acme.com" },
            new() { Id = Guid.NewGuid(), CustomerRequestNumber = "CUS-2026-000002", CompanyName = "Other", LegalName = "acme inc", EmailAddress = "x@y.com" },
        };
        _requests.Setup(r => r.FindStep1DuplicatesAsync(
                tenantId, candidate.Id, candidate.CompanyName, candidate.LegalName, candidate.EmailAddress, It.IsAny<CancellationToken>()))
            .ReturnsAsync(matches);

        var result = await Create().CheckStep1Async(tenantId, candidate, default);

        result.Should().HaveCount(2);
        result[0].MatchedFields.Should().BeEquivalentTo(new[] { "Company Name", "Email Address" });
        result[1].MatchedFields.Should().BeEquivalentTo(new[] { "Legal Name" });

        // Tenant/exclude scoping flows through to the repo exactly as supplied.
        _requests.Verify(r => r.FindStep1DuplicatesAsync(
            tenantId, candidate.Id, "Acme", "Acme Inc", "billing@acme.com", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckTaxNumber_returns_matches()
    {
        var tenantId = Guid.NewGuid();
        var excludeId = Guid.NewGuid();
        _requests.Setup(r => r.FindTaxNumberDuplicatesAsync(tenantId, excludeId, "TAX-99", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CustomerRequest>
            {
                new() { Id = Guid.NewGuid(), CustomerRequestNumber = "CUS-2026-000005", CompanyName = "Beta", TaxNumber = "TAX-99" },
            });

        var result = await Create().CheckTaxNumberAsync(tenantId, excludeId, "  TAX-99  ", default);

        result.Should().HaveCount(1);
        result[0].MatchedFields.Should().BeEquivalentTo(new[] { "Tax Number" });
        result[0].CompanyName.Should().Be("Beta");
        // Trimmed before hitting the repo.
        _requests.Verify(r => r.FindTaxNumberDuplicatesAsync(tenantId, excludeId, "TAX-99", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CheckTaxNumber_with_blank_returns_empty_without_hitting_repo(string taxNumber)
    {
        var result = await Create().CheckTaxNumberAsync(Guid.NewGuid(), Guid.NewGuid(), taxNumber, default);

        result.Should().BeEmpty();
        _requests.Verify(r => r.FindTaxNumberDuplicatesAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
