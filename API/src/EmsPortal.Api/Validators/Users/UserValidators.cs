using FluentValidation;
using EmsPortal.Api.Models.Users;
using EmsPortal.Domain.Enums;

namespace EmsPortal.Api.Validators.Users;

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        // A user is created by promoting an existing Person (WO-61).
        RuleFor(x => x.PersonId).NotEmpty().WithMessage("personId is required.");
        // Email is optional here — it defaults to the person's primary email when omitted.
        RuleFor(x => x.Email).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x)
            .Must(x => x.RoleId is not null || !string.IsNullOrWhiteSpace(x.Role))
            .WithMessage("Either role or roleId is required.");
        RuleFor(x => x.Role)
            .Must(role => Enum.TryParse<UserRole>(role, ignoreCase: false, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.Role))
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

public sealed class AssignTenantRoleRequestValidator : AbstractValidator<AssignTenantRoleRequest>
{
    public AssignTenantRoleRequestValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x)
            .Must(x => x.RoleId is not null || !string.IsNullOrWhiteSpace(x.Role))
            .WithMessage("Either role or roleId is required.");
        RuleFor(x => x.Role)
            .Must(role => Enum.TryParse<UserRole>(role, ignoreCase: false, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.Role))
            .WithMessage("Role must be one of: SuperAdmin, TenantAdmin, Operator.");
    }
}
