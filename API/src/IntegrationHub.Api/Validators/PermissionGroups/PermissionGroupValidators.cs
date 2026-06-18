using FluentValidation;
using IntegrationHub.Api.Models.PermissionGroups;

namespace IntegrationHub.Api.Validators.PermissionGroups;

public sealed class CreateGroupRequestValidator : AbstractValidator<CreateGroupRequest>
{
    public CreateGroupRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class UpdateGroupRequestValidator : AbstractValidator<UpdateGroupRequest>
{
    public UpdateGroupRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
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
