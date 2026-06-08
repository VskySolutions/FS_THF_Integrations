using IntegrationHub.Application.Abstractions.Connectors;
using IntegrationHub.Application.Abstractions.Connectors.Concur;
using IntegrationHub.Shared.Connectors;

namespace IntegrationHub.Application.Concur;

/// <summary>Validates a Concur vendor payment payload (non-short-circuiting).</summary>
public sealed class PaymentValidator : IValidator<ConcurVendorPayment>
{
    public ValidationResult Validate(ConcurVendorPayment payload)
    {
        var violations = new List<ValidationViolation>();

        if (string.IsNullOrWhiteSpace(payload.PaymentId))
        {
            violations.Add(new ValidationViolation(nameof(payload.PaymentId), "Payment id is required."));
        }

        if (string.IsNullOrWhiteSpace(payload.InvoiceId))
        {
            violations.Add(new ValidationViolation(nameof(payload.InvoiceId), "Referenced invoice id is required."));
        }

        if (payload.Amount < 0)
        {
            violations.Add(new ValidationViolation(nameof(payload.Amount), "Amount cannot be negative."));
        }

        return violations.Count == 0 ? ValidationResult.Valid() : ValidationResult.Invalid(violations);
    }
}
