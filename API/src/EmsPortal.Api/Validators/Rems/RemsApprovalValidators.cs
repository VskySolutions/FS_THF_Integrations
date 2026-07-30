using EmsPortal.Api.Models.Rems;
using FluentValidation;

namespace EmsPortal.Api.Validators.Rems;

/// <summary>Validates a task rejection (WO-114, AC-REMS-020.1): a reason is required.</summary>
public sealed class RejectApprovalTaskRequestValidator : AbstractValidator<RejectApprovalTaskRequest>
{
    public RejectApprovalTaskRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A rejection reason is required.")
            .MaximumLength(500);
    }
}
