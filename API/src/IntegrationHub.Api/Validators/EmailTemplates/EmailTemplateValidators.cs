using FluentValidation;
using IntegrationHub.Api.Models.EmailTemplates;

namespace IntegrationHub.Api.Validators.EmailTemplates;

public sealed class SaveEmailTemplateRequestValidator : AbstractValidator<SaveEmailTemplateRequest>
{
    public SaveEmailTemplateRequestValidator()
    {
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Body).NotEmpty();
    }
}
