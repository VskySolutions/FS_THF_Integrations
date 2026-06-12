using FluentValidation;
using IntegrationHub.Api.Models.Persons;

namespace IntegrationHub.Api.Validators.Persons;

public sealed class CreatePersonRequestValidator : AbstractValidator<CreatePersonRequest>
{
    public CreatePersonRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DisplayName).MaximumLength(200).When(x => x.DisplayName is not null);
        RuleFor(x => x.PrimaryEmail).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.PrimaryEmail));
        RuleFor(x => x.SecondaryEmail).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.SecondaryEmail));
        RuleFor(x => x.MobileNumber).MaximumLength(32).When(x => x.MobileNumber is not null);
    }
}

public sealed class UpdatePersonRequestValidator : AbstractValidator<UpdatePersonRequest>
{
    public UpdatePersonRequestValidator()
    {
        RuleFor(x => x.FirstName).MaximumLength(100).When(x => x.FirstName is not null);
        RuleFor(x => x.LastName).MaximumLength(100).When(x => x.LastName is not null);
        RuleFor(x => x.DisplayName).MaximumLength(200).When(x => x.DisplayName is not null);
        RuleFor(x => x.PrimaryEmail).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.PrimaryEmail));
        RuleFor(x => x.SecondaryEmail).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.SecondaryEmail));
    }
}
