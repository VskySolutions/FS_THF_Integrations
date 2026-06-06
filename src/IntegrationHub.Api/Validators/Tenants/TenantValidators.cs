using FluentValidation;
using IntegrationHub.Api.Models.Tenants;
using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Api.Validators.Tenants;

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

public sealed class ConcurCredentialsRequestValidator : AbstractValidator<ConcurCredentialsRequest>
{
    public ConcurCredentialsRequestValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.ClientSecret).NotEmpty();
        RuleFor(x => x.BaseUrl).NotEmpty();
        RuleFor(x => x.CompanyUuid).NotEmpty();
    }
}

public sealed class MaconomyCredentialsRequestValidator : AbstractValidator<MaconomyCredentialsRequest>
{
    public MaconomyCredentialsRequestValidator()
    {
        RuleFor(x => x.BaseUrl).NotEmpty();
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class CreateMappingRequestValidator : AbstractValidator<CreateMappingRequest>
{
    public CreateMappingRequestValidator()
    {
        RuleFor(x => x.SourceSystem).Must(BeSystem).WithMessage("Invalid source system.");
        RuleFor(x => x.DestinationSystem).Must(BeSystem).WithMessage("Invalid destination system.");
        RuleFor(x => x.SourceField).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DestinationField).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TransformationRule).MaximumLength(2000);
    }

    private static bool BeSystem(string value) => Enum.TryParse<SystemName>(value, ignoreCase: false, out _);
}
