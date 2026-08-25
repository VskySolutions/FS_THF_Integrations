using EmsPortal.Api.Models.Rems;
using FluentValidation;

namespace EmsPortal.Api.Validators.Rems;

/// <summary>
/// Validates the EMS form build/save payload (WO-112, AC-REMS-007.7): a CSE and a known industry-group
/// code are both required before the form can be saved.
/// </summary>
public sealed class SaveRemsFormRequestValidator : AbstractValidator<SaveRemsFormRequest>
{
    public SaveRemsFormRequestValidator()
    {
        RuleFor(x => x.CseUserId).NotEmpty().WithMessage("cseUserId is required.");
        RuleFor(x => x.IndustryGroup)
            .Must(RemsFormOptionCodes.IsKnownIndustryGroup)
            .WithMessage($"industryGroup must be one of: {string.Join(", ", RemsFormOptionCodes.IndustryGroups)}.");
    }
}

/// <summary>
/// The seeded <c>REMS.IndustryGroup</c> option-set codes (see <c>DefaultOptionSets</c>). The industry
/// group is trivially closed so it is validated against the known codes.
/// </summary>
internal static class RemsFormOptionCodes
{
    // "business" stays accepted though it is no longer offered: forms sent before it was split into
    // not-for-profit / insurance / commercial still carry it, and those must remain completable.
    // "trust_estate" asks the business questions too — see RemsFormPayloadValidator.BusinessGroups.
    public static readonly IReadOnlyList<string> IndustryGroups =
        new[] { "individual", "not_for_profit", "insurance", "commercial", "trust_estate", "government", "business" };

    public static bool IsKnownIndustryGroup(string? value) => value is not null && IndustryGroups.Contains(value);
}
