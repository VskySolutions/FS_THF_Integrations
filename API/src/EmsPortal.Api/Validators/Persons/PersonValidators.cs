using FluentValidation;
using EmsPortal.Api.Models.Persons;

namespace EmsPortal.Api.Validators.Persons;

public sealed class CreatePersonRequestValidator : AbstractValidator<CreatePersonRequest>
{
    public CreatePersonRequestValidator()
    {
        // The names are held to what a name is (see PersonNames) as well as to the column width: a Person
        // is what everything else is filed under, and "test123" here is a client nobody finds again.
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100).MustBeAPersonName("First name");
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100).MustBeAPersonName("Last name");
        RuleFor(x => x.MiddleName).MaximumLength(100).MustBeAPersonName("Middle name").When(x => x.MiddleName is not null);
        RuleFor(x => x.PreferredName).MaximumLength(100).MustBeAPersonName("Preferred name").When(x => x.PreferredName is not null);
        RuleFor(x => x.DisplayName).MaximumLength(200).When(x => x.DisplayName is not null);
        // Primary email is mandatory when creating a person (it seeds the login username on promotion).
        RuleFor(x => x.PrimaryEmail).NotEmpty().WithMessage("Primary email is required.").EmailAddress().MaximumLength(256);
        RuleFor(x => x.SecondaryEmail).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.SecondaryEmail));
        RuleFor(x => x.MobileNumber).MaximumLength(32).When(x => x.MobileNumber is not null);
    }
}

public sealed class UpdatePersonRequestValidator : AbstractValidator<UpdatePersonRequest>
{
    public UpdatePersonRequestValidator()
    {
        RuleFor(x => x.FirstName).MaximumLength(100).MustBeAPersonName("First name").When(x => x.FirstName is not null);
        RuleFor(x => x.LastName).MaximumLength(100).MustBeAPersonName("Last name").When(x => x.LastName is not null);
        RuleFor(x => x.MiddleName).MaximumLength(100).MustBeAPersonName("Middle name").When(x => x.MiddleName is not null);
        RuleFor(x => x.PreferredName).MaximumLength(100).MustBeAPersonName("Preferred name").When(x => x.PreferredName is not null);
        RuleFor(x => x.DisplayName).MaximumLength(200).When(x => x.DisplayName is not null);
        RuleFor(x => x.PrimaryEmail).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.PrimaryEmail));
        RuleFor(x => x.SecondaryEmail).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.SecondaryEmail));
    }
}
