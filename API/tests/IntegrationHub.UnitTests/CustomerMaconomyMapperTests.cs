using FluentAssertions;
using IntegrationHub.Application.Abstractions.Connectors;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Customers;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using Moq;

namespace IntegrationHub.UnitTests;

// Customer → Maconomy field mapping: applies the tenant's CustomerSync mapping rules with code defaults.
public class CustomerMaconomyMapperTests
{
    private readonly Mock<IMappingConfigurationRepository> _mappings = new();
    private readonly Mock<ITransformationRuleEvaluator> _evaluator = new();

    private CustomerMaconomyMapper Create() => new(_mappings.Object, _evaluator.Object);

    private static CustomerRequest Request() => new()
    {
        Id = Guid.NewGuid(),
        CompanyName = "Acme",
        LegalName = "Acme Incorporated",
        EmailAddress = "billing@acme.com",
        Country = "US",
        AddressLine1 = "1 Main St",
        TaxNumber = "TAX-1",
        CreditLimit = 5000m,
    };

    private void RulesReturn(params MappingConfiguration[] rules)
        => _mappings.Setup(m => m.GetActiveForFlowAsync(SystemName.Platform, SystemName.Maconomy, CustomerMaconomyMapper.InterfaceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules);

    private void EvaluatorPassThrough()
        => _evaluator.Setup(e => e.Evaluate(It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<IReadOnlyDictionary<string, object?>>()))
            .Returns((string? _, object? value, IReadOnlyDictionary<string, object?> _) => value);

    [Fact]
    public async Task With_no_rules_applies_default_mapping()
    {
        RulesReturn();

        var payload = await Create().BuildAsync(Request());

        payload.Name.Should().Be("Acme");                  // default: CompanyName → Name
        payload.LegalName.Should().Be("Acme Incorporated");
        payload.Country.Should().Be("US");
        payload.AddressLine1.Should().Be("1 Main St");
        payload.TaxNumber.Should().Be("TAX-1");
        payload.CreditLimit.Should().Be(5000m);
    }

    [Fact]
    public async Task A_rule_overrides_the_default_for_its_target_field()
    {
        // Map LegalName → Name instead of the default CompanyName → Name.
        RulesReturn(new MappingConfiguration
        {
            Id = Guid.NewGuid(),
            SourceSystem = SystemName.Platform,
            TargetSystem = SystemName.Maconomy,
            InterfaceName = CustomerMaconomyMapper.InterfaceName,
            SourceField = nameof(CustomerRequest.LegalName),
            DestinationField = "Name",
            IsActive = true,
        });
        EvaluatorPassThrough();

        var payload = await Create().BuildAsync(Request());

        payload.Name.Should().Be("Acme Incorporated");      // overridden by the rule
        payload.LegalName.Should().Be("Acme Incorporated"); // still defaulted
    }

    [Fact]
    public async Task Transformation_rule_is_applied_via_the_evaluator()
    {
        RulesReturn(new MappingConfiguration
        {
            Id = Guid.NewGuid(),
            SourceSystem = SystemName.Platform,
            TargetSystem = SystemName.Maconomy,
            InterfaceName = CustomerMaconomyMapper.InterfaceName,
            SourceField = nameof(CustomerRequest.CompanyName),
            DestinationField = "Name",
            TransformationRule = "uppercase",
            IsActive = true,
        });
        _evaluator.Setup(e => e.Evaluate("uppercase", It.IsAny<object?>(), It.IsAny<IReadOnlyDictionary<string, object?>>()))
            .Returns((string? _, object? value, IReadOnlyDictionary<string, object?> _) => (value as string)?.ToUpperInvariant());

        var payload = await Create().BuildAsync(Request());

        payload.Name.Should().Be("ACME");
    }
}
