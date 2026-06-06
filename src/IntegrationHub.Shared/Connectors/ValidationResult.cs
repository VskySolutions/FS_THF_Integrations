namespace IntegrationHub.Shared.Connectors;

/// <summary>
/// Result of an <c>IValidator</c> invocation. Validation is non-short-circuiting:
/// all violations are reported in a single pass (AC-COF-003.2).
/// </summary>
public sealed class ValidationResult
{
    public bool IsValid { get; init; }

    public IReadOnlyList<ValidationViolation> Violations { get; init; } = Array.Empty<ValidationViolation>();

    public static ValidationResult Valid() => new() { IsValid = true };

    public static ValidationResult Invalid(IReadOnlyList<ValidationViolation> violations)
        => new() { IsValid = false, Violations = violations };
}

/// <summary>A single validation violation: the offending field and a message.</summary>
public sealed record ValidationViolation(string Field, string Message);
