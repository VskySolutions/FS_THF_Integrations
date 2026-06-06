using FluentValidation;
using IntegrationHub.Api.Models.Users;
using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Api.Validators.Users;

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Role)
            .NotEmpty()
            .Must(role => Enum.TryParse<UserRole>(role, ignoreCase: false, out _))
            .WithMessage("Role must be one of: SuperAdmin, TenantAdmin, Operator.");
    }
}

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.DisplayName).MaximumLength(200).When(x => x.DisplayName is not null);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(256).When(x => x.Email is not null);
        RuleFor(x => x).Must(x => x.DisplayName is not null || x.Email is not null)
            .WithMessage("At least one of displayName or email must be provided.");
    }
}

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
    }
}
