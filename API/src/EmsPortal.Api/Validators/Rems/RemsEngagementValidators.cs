using EmsPortal.Api.Models.Rems;
using FluentValidation;

namespace EmsPortal.Api.Validators.Rems;

/// <summary>Validates a client-record edit (WO-114). The client email is not editable and is absent from the payload.</summary>
public sealed class UpdateRemsClientRequestValidator : AbstractValidator<UpdateRemsClientRequest>
{
    public UpdateRemsClientRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).When(x => x.Name is not null);
        RuleFor(x => x.MobileNumber).MaximumLength(32).When(x => !string.IsNullOrWhiteSpace(x.MobileNumber));
        RuleFor(x => x.ReferralSource).MaximumLength(64).When(x => !string.IsNullOrWhiteSpace(x.ReferralSource));
        RuleFor(x => x.BillingContactName).MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.BillingContactName));
        RuleFor(x => x.BillingEmail).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.BillingEmail));
    }
}

/// <summary>Validates an entity contacts replace (WO-114): each contact carries a role.</summary>
public sealed class UpdateRemsEntityContactsRequestValidator : AbstractValidator<UpdateRemsEntityContactsRequest>
{
    public UpdateRemsEntityContactsRequestValidator()
    {
        RuleForEach(x => x.Contacts).ChildRules(c =>
        {
            c.RuleFor(i => i.Role).NotEmpty().WithMessage("contact role is required.").MaximumLength(64);
            c.RuleFor(i => i.Name).MaximumLength(200).When(i => !string.IsNullOrWhiteSpace(i.Name));
            c.RuleFor(i => i.Email).EmailAddress().MaximumLength(256).When(i => !string.IsNullOrWhiteSpace(i.Email));
            c.RuleFor(i => i.Phone).MaximumLength(32).When(i => !string.IsNullOrWhiteSpace(i.Phone));
        });
    }
}

/// <summary>
/// Validates an engagement update (WO-114): realization is 0–100, the fee estimate is non-negative, and
/// the bill count is a whole number above zero — the last mirrors the
/// <c>CK_REMSEngagement_NumberOfBills</c> check constraint, so a bad value is refused with a message
/// rather than by the database.
/// </summary>
public sealed class UpdateRemsEngagementRequestValidator : AbstractValidator<UpdateRemsEngagementRequest>
{
    public UpdateRemsEngagementRequestValidator()
    {
        RuleFor(x => x.Department).MaximumLength(64).When(x => x.Department is not null);
        RuleFor(x => x.ServiceLine).MaximumLength(64).When(x => x.ServiceLine is not null);
        RuleFor(x => x.SubServiceLine).MaximumLength(64).When(x => x.SubServiceLine is not null);
        RuleFor(x => x.SubIndustry).MaximumLength(64).When(x => x.SubIndustry is not null);
        RuleFor(x => x.BillingPeriod).MaximumLength(64).When(x => x.BillingPeriod is not null);
        RuleFor(x => x.FirstYearFeeEstimate)
            .GreaterThanOrEqualTo(0).WithMessage("firstYearFeeEstimate must be zero or greater.")
            .When(x => x.FirstYearFeeEstimate.HasValue);
        RuleFor(x => x.RealizationPercentage)
            .InclusiveBetween(0, 100).WithMessage("realizationPercentage must be between 0 and 100.")
            .When(x => x.RealizationPercentage.HasValue);
        RuleFor(x => x.NumberOfBills)
            .GreaterThan(0).WithMessage("numberOfBills must be greater than zero.")
            .When(x => x.NumberOfBills.HasValue);
    }
}

/// <summary>Validates linking a signed client-acceptance form (WO-114).</summary>
public sealed class LinkClientAcceptanceFormRequestValidator : AbstractValidator<LinkClientAcceptanceFormRequest>
{
    public LinkClientAcceptanceFormRequestValidator()
    {
        RuleFor(x => x.MediaId).NotEmpty().WithMessage("mediaId is required.");
    }
}

/// <summary>Validates a government-audit detail update (WO-114).</summary>
public sealed class UpdateRemsGovernmentDetailRequestValidator : AbstractValidator<UpdateRemsGovernmentDetailRequest>
{
    public UpdateRemsGovernmentDetailRequestValidator()
    {
        RuleFor(x => x.ContractNumber).MaximumLength(64).When(x => !string.IsNullOrWhiteSpace(x.ContractNumber));
        RuleFor(x => x.OriginalTerm).MaximumLength(500).When(x => !string.IsNullOrWhiteSpace(x.OriginalTerm));
        RuleFor(x => x.RenewalTerms).MaximumLength(500).When(x => !string.IsNullOrWhiteSpace(x.RenewalTerms));
    }
}

/// <summary>Validates a tax-detail update (WO-114): no duplicate tax-form ids.</summary>
public sealed class UpdateRemsTaxDetailRequestValidator : AbstractValidator<UpdateRemsTaxDetailRequest>
{
    public UpdateRemsTaxDetailRequestValidator()
    {
        RuleFor(x => x.TaxFormIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("taxFormIds must not contain duplicates.");
    }
}

/// <summary>Validates setting marketing tags (WO-114, AC-REMS-017.4): at least one tag is required to save.</summary>
public sealed class SetRemsMarketingRequestValidator : AbstractValidator<SetRemsMarketingRequest>
{
    public SetRemsMarketingRequestValidator()
    {
        RuleFor(x => x.MarketingMethodIds)
            .NotEmpty().WithMessage("At least one marketing tag is required.");
        RuleFor(x => x.MarketingMethodIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("marketingMethodIds must not contain duplicates.")
            .When(x => x.MarketingMethodIds.Count > 0);
    }
}

/// <summary>
/// Validates setting commission splits (WO-114, AC-REMS-016.1/2): up to ten distinct recipients, each with
/// a percentage greater than 0 and at most 100, and no more than 100% allocated in total.
/// </summary>
public sealed class SetRemsCommissionRequestValidator : AbstractValidator<SetRemsCommissionRequest>
{
    public SetRemsCommissionRequestValidator()
    {
        RuleFor(x => x.Splits)
            .Must(s => s.Count <= 10).WithMessage("At most ten commission recipients are allowed.");
        RuleFor(x => x.Splits)
            .Must(s => s.Select(i => i.EmployeeId).Distinct().Count() == s.Count)
            .WithMessage("A recipient may appear only once.")
            .When(x => x.Splits.Count > 0);
        // The splits divide a single commission, so the whole set can never exceed it. Rounded to 2dp so
        // three 33.33/33.34 shares are accepted as exactly 100% rather than rejected on decimal noise.
        RuleFor(x => x.Splits)
            .Must(s => Math.Round(s.Sum(i => i.Percentage), 2) <= 100m)
            .WithMessage("The commission splits cannot add up to more than 100% in total.")
            .When(x => x.Splits.Count > 0);
        RuleForEach(x => x.Splits).ChildRules(s =>
        {
            s.RuleFor(i => i.EmployeeId).NotEmpty().WithMessage("employeeId is required.");
            s.RuleFor(i => i.Percentage)
                .GreaterThan(0).WithMessage("percentage must be greater than 0.")
                .LessThanOrEqualTo(100).WithMessage("percentage must be at most 100.");
        });
    }
}
