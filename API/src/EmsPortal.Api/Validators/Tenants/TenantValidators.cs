using FluentValidation;
using EmsPortal.Api.Models.Tenants;

namespace EmsPortal.Api.Validators.Tenants;

public sealed class CreateTenantRequestValidator : AbstractValidator<CreateTenantRequest>
{
    public CreateTenantRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Identifier)
            .NotEmpty()
            .MaximumLength(100)
            .Matches("^[a-z0-9-]+$")
            .WithMessage("Identifier must be a URL-safe slug (lowercase letters, digits, hyphens).");
    }
}

public sealed class UpdateTenantRequestValidator : AbstractValidator<UpdateTenantRequest>
{
    public UpdateTenantRequestValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
}
