using FluentValidation;
using IntegrationHub.Api.Models.Customers;

namespace IntegrationHub.Api.Validators.Customers;

public sealed class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerRequestValidator()
    {
        RuleFor(x => x.LegalName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EmailAddress).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Country).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AddressLine1).NotEmpty().MaximumLength(256);
        RuleFor(x => x.PhoneNumber).MaximumLength(32);
        RuleFor(x => x.Website).MaximumLength(512);
    }
}

public sealed class UpdateCustomerRequestValidator : AbstractValidator<UpdateCustomerRequest>
{
    public UpdateCustomerRequestValidator()
    {
        RuleFor(x => x.LegalName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EmailAddress).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Country).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AddressLine1).NotEmpty().MaximumLength(256);
    }
}

public sealed class RejectCustomerRequestValidator : AbstractValidator<RejectCustomerRequest>
{
    public RejectCustomerRequestValidator()
        => RuleFor(x => x.Reason).NotEmpty().WithMessage("A rejection reason is required.").MaximumLength(2000);
}

public sealed class ReturnCustomerRequestValidator : AbstractValidator<ReturnCustomerRequest>
{
    public ReturnCustomerRequestValidator()
        => RuleFor(x => x.Notes).NotEmpty().WithMessage("Correction notes are required.").MaximumLength(2000);
}
