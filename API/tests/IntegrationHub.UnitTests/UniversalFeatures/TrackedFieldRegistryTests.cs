using FluentAssertions;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Infrastructure.Persistence.ModifiedLog;

namespace IntegrationHub.UnitTests.UniversalFeatures;

// WO-98 / WO-100: the [TrackedField] registry discovers the initial CustomerRequest fields.
public class TrackedFieldRegistryTests
{
    [Theory]
    [InlineData("CustomerRequest.CreditLimit", true)]
    [InlineData("CustomerRequest.PaymentTerms", true)]
    [InlineData("CustomerRequest.RiskCategory", false)]
    public void Registers_initial_customer_request_fields(string key, bool isSystemTracked)
    {
        var descriptor = TrackedFieldRegistry.GetByKey(key);

        descriptor.Should().NotBeNull();
        descriptor!.EntityType.Should().Be(EntityType.CustomerRequest);
        descriptor.IsSystemTracked.Should().Be(isSystemTracked);
    }

    [Fact]
    public void Groups_fields_by_entity_type()
    {
        var fields = TrackedFieldRegistry.ForEntityType(EntityType.CustomerRequest);

        fields.Select(f => f.PropertyName).Should().Contain(new[] { "CreditLimit", "PaymentTerms", "RiskCategory" });
    }

    [Fact]
    public void Unknown_key_returns_null() => TrackedFieldRegistry.GetByKey("CustomerRequest.Nope").Should().BeNull();
}
