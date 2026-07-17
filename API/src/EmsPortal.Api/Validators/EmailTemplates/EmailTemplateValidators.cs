using FluentValidation;
using EmsPortal.Api.Models.EmailTemplates;

namespace EmsPortal.Api.Validators.EmailTemplates;

public sealed class SaveEmailTemplateRequestValidator : AbstractValidator<SaveEmailTemplateRequest>
{
    public SaveEmailTemplateRequestValidator()
    {
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Body).NotEmpty();
    }
}
