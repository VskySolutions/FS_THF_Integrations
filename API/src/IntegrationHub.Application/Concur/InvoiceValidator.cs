using IntegrationHub.Application.Abstractions.Connectors;
using IntegrationHub.Application.Abstractions.Connectors.Concur;
using IntegrationHub.Shared.Connectors;

namespace IntegrationHub.Application.Concur;

/// <summary>Validates a Concur vendor invoice header and its lines (non-short-circuiting).</summary>
public sealed class InvoiceValidator : IValidator<ConcurVendorInvoice>
{
    public ValidationResult Validate(ConcurVendorInvoice payload)
    {
        var violations = new List<ValidationViolation>();

        if (string.IsNullOrWhiteSpace(payload.InvoiceNumber))
        {
            violations.Add(new ValidationViolation(nameof(payload.InvoiceNumber), "Invoice number is required."));
        }

        if (string.IsNullOrWhiteSpace(payload.VendorId))
        {
            violations.Add(new ValidationViolation(nameof(payload.VendorId), "Vendor id is required."));
        }

        if (payload.Amount < 0)
        {
            violations.Add(new ValidationViolation(nameof(payload.Amount), "Amount cannot be negative."));
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
