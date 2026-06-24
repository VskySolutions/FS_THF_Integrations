using FluentAssertions;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Infrastructure.Persistence.ModifiedLog;

namespace IntegrationHub.UnitTests.UniversalFeatures;

// WO-98: Modified Log value formatting (enum→label, bool→Yes/No, decimal→number).
public class FieldValueFormatterTests
{
    private readonly FieldValueFormatter _formatter = new();

    [Fact]
    public void Null_formats_as_null() => _formatter.Format(null).Should().BeNull();

    [Theory]
    [InlineData(true, "Yes")]
    [InlineData(false, "No")]
    public void Bool_formats_as_yes_no(bool value, string expected) => _formatter.Format(value).Should().Be(expected);

    [Fact]
    public void Decimal_formats_with_two_places() => _formatter.Format(50000m).Should().Be("50,000.00");

    [Fact]
    public void Enum_formats_as_spaced_label()
        => _formatter.Format(CustomerRequestStatus.PendingApproval).Should().Be("Pending Approval");

    [Fact]
    public void String_formats_unchanged() => _formatter.Format("Low").Should().Be("Low");
}
