using IntegrationHub.Shared.Connectors;

namespace IntegrationHub.Application.Abstractions.Connectors;

/// <summary>
/// Contract for validating a domain payload against business rules before processing
/// (REQ-COF-003). Returns all violations in a single non-short-circuiting pass. Distinct
/// from the FluentValidation DTO validation used at the API layer.
/// </summary>
public interface IValidator<T>
{
    ValidationResult Validate(T payload);
}
