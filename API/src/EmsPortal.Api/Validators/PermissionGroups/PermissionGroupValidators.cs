using FluentValidation;
using EmsPortal.Api.Models.PermissionGroups;

namespace EmsPortal.Api.Validators.PermissionGroups;

public sealed class CreateGroupRequestValidator : AbstractValidator<CreateGroupRequest>
{
    public CreateGroupRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
        // Capacity limit (WO-119): optional, but a supplied value must be non-negative (0 = no members allowed).
        RuleFor(x => x.CapacityLimit).GreaterThanOrEqualTo(0)
            .When(x => x.CapacityLimit.HasValue)
            .WithMessage("Capacity limit cannot be negative.");
    }
}

public sealed class UpdateGroupRequestValidator : AbstractValidator<UpdateGroupRequest>
{
    public UpdateGroupRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.CapacityLimit).GreaterThanOrEqualTo(0)
            .When(x => x.CapacityLimit.HasValue)
            .WithMessage("Capacity limit cannot be negative.");
    }
}

public sealed class SaveTemplateRequestValidator : AbstractValidator<SaveTemplateRequest>
{
    public SaveTemplateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class AssignGroupsRequestValidator : AbstractValidator<AssignGroupsRequest>
{
    public AssignGroupsRequestValidator()
        => RuleFor(x => x.GroupIds).NotEmpty().WithMessage("At least one group is required.");
}
