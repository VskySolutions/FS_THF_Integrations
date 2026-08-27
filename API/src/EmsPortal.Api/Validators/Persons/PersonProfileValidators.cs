using FluentValidation;
using EmsPortal.Api.Models.Profile;

namespace EmsPortal.Api.Validators.Persons;

/// <summary>
/// The self-service profile payload (and the admin's edit of somebody else's). Every field is optional —
/// null means "leave it alone" — so the rules only fire on what was actually sent. The names are held to
/// <see cref="PersonNames"/>, the same shape the People screens and the REMS intake form enforce: this
/// endpoint writes the same Person columns they do, and a rule that stopped at one door is not a rule.
/// </summary>
public sealed class UpdatePersonProfileRequestValidator : AbstractValidator<UpdatePersonProfileRequest>
{
    public UpdatePersonProfileRequestValidator()
    {
        RuleFor(x => x.FirstName).MaximumLength(100).MustBeAPersonName("First name").When(x => x.FirstName is not null);
        RuleFor(x => x.MiddleName).MaximumLength(100).MustBeAPersonName("Middle name").When(x => x.MiddleName is not null);
        RuleFor(x => x.LastName).MaximumLength(100).MustBeAPersonName("Last name").When(x => x.LastName is not null);
        RuleFor(x => x.PreferredName).MaximumLength(100).MustBeAPersonName("Preferred name").When(x => x.PreferredName is not null);
        RuleFor(x => x.DisplayName).MaximumLength(200).When(x => x.DisplayName is not null);
    }
}
