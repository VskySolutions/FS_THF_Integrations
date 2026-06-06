using IntegrationHub.Application.Abstractions.Connectors;
using IntegrationHub.Application.Abstractions.Connectors.Concur;
using IntegrationHub.Shared.Connectors;

namespace IntegrationHub.Application.Concur;

/// <summary>
/// Validates a Concur expense report header and its lines, returning all violations in a
/// single pass (REQ-COF-003). Domain validation, distinct from API DTO validation.
/// </summary>
public sealed class ExpenseValidator : IValidator<ConcurExpenseReport>
{
    public ValidationResult Validate(ConcurExpenseReport payload)
    {
        var violations = new List<ValidationViolation>();

        if (string.IsNullOrWhiteSpace(payload.ReportId))
        {
            violations.Add(new ValidationViolation(nameof(payload.ReportId), "Report id is required."));
        }

        if (string.IsNullOrWhiteSpace(payload.EmployeeId))
        {
            violations.Add(new ValidationViolation(nameof(payload.EmployeeId), "Employee id is required."));
        }

        if (payload.TotalAmount < 0)
        {
            violations.Add(new ValidationViolation(nameof(payload.TotalAmount), "Total amount cannot be negative."));
        }

        foreach (var line in payload.Lines)
        {
            if (line.Amount < 0)
            {
                violations.Add(new ValidationViolation($"Line[{line.LineId}].Amount", "Line amount cannot be negative."));
            }
        }

        return violations.Count == 0 ? ValidationResult.Valid() : ValidationResult.Invalid(violations);
    }
}
