using FluentAssertions;
using IntegrationHub.Application.Abstractions.Connectors.Concur;
using IntegrationHub.Application.Concur;
using IntegrationHub.Infrastructure.Connectors;
using IntegrationHub.Infrastructure.Security;
using IntegrationHub.Shared.Configuration;

namespace IntegrationHub.UnitTests;

// WO-32 / WO-34: pure-logic units (validators, rule evaluator, backoff, hashing, error normalization).

public class ExpenseValidatorTests
{
    private readonly ExpenseValidator _validator = new();

    [Fact]
    public void Valid_report_passes()
    {
        var report = new ConcurExpenseReport("R1", "E1", "Approved", DateTime.UtcNow, 100m, "USD",
            new[] { new ConcurExpenseLine("L1", "Meals", 50m, DateTime.UtcNow, "lunch") });

        _validator.Validate(report).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Missing_required_fields_reports_all_violations()
    {
        var report = new ConcurExpenseReport("", "", "Approved", null, -5m, "USD", Array.Empty<ConcurExpenseLine>());

        var result = _validator.Validate(report);

        result.IsValid.Should().BeFalse();
        result.Violations.Should().HaveCountGreaterThanOrEqualTo(3); // reportId, employeeId, amount
    }

    [Fact]
    public void Negative_line_amount_is_a_violation()
    {
        var report = new ConcurExpenseReport("R1", "E1", "Approved", DateTime.UtcNow, 10m, "USD",
            new[] { new ConcurExpenseLine("L1", "Meals", -1m, null, null) });

        _validator.Validate(report).IsValid.Should().BeFalse();
    }
}

public class TransformationRuleEvaluatorTests
{
    private readonly TransformationRuleEvaluator _evaluator = new();
    private static readonly IReadOnlyDictionary<string, object?> NoFields = new Dictionary<string, object?>();

    [Fact]
    public void Null_rule_passes_value_through()
        => _evaluator.Evaluate(null, "abc", NoFields).Should().Be("abc");

    [Fact]
    public void Date_rule_reformats()
        => _evaluator.Evaluate("date:yyyy-MM-dd|MM/dd/yyyy", "2026-01-15", NoFields).Should().Be("01/15/2026");

    [Fact]
    public void Lookup_rule_maps_value()
        => _evaluator.Evaluate("lookup:A=1;B=2", "B", NoFields).Should().Be("2");

    [Fact]
    public void Lookup_rule_uses_default_when_unmatched()
        => _evaluator.Evaluate("lookup:A=1;default=9", "Z", NoFields).Should().Be("9");

    [Fact]
    public void Concat_rule_joins_fields_and_literals()
    {
        var fields = new Dictionary<string, object?> { ["first"] = "Jane", ["last"] = "Doe" };
        _evaluator.Evaluate("concat:first,' ',last", null, fields).Should().Be("Jane Doe");
    }
}

public class RetryOptionsTests
{
    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 15)]
    [InlineData(3, 30)]
    [InlineData(4, 60)]
    [InlineData(7, 60)] // clamps to last
    public void Backoff_follows_5_15_30_60_strategy(int attempt, int expectedMinutes)
        => new RetryOptions().GetBackoff(attempt).Should().Be(TimeSpan.FromMinutes(expectedMinutes));
}

public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_then_verify_roundtrips()
    {
        var (hash, salt) = _hasher.Hash("Secret123");
        _hasher.Verify("Secret123", hash, salt).Should().BeTrue();
    }

    [Fact]
    public void Wrong_password_fails_verification()
    {
        var (hash, salt) = _hasher.Hash("Secret123");
        _hasher.Verify("Wrong", hash, salt).Should().BeFalse();
    }

    [Fact]
    public void Generated_temporary_password_meets_complexity()
    {
        var pwd = _hasher.GenerateTemporaryPassword();
        pwd.Length.Should().BeGreaterThanOrEqualTo(8);
        pwd.Should().MatchRegex("[A-Z]").And.MatchRegex("[0-9]");
    }
}

public class ConnectorErrorTests
{
    [Theory]
    [InlineData(500, true)]
    [InlineData(503, true)]
    [InlineData(408, true)]
    [InlineData(429, true)]
    [InlineData(400, false)]
    [InlineData(401, false)]
    [InlineData(404, false)]
    public void Status_codes_map_to_correct_retriable_flag(int statusCode, bool retriable)
    {
        var result = ConnectorError.FromStatusCode<bool>("Concur", "Fetch", (System.Net.HttpStatusCode)statusCode, "body");
        result.Success.Should().BeFalse();
        result.IsRetriable.Should().Be(retriable);
    }

    [Fact]
    public void Network_exception_is_retriable()
    {
        var result = ConnectorError.FromException<bool>("Concur", "Fetch", new HttpRequestException("boom"));
        result.IsRetriable.Should().BeTrue();
    }
}
