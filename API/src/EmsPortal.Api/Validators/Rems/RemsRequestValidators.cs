using EmsPortal.Api.Models.Rems;
using EmsPortal.Domain.Entities;
using FluentValidation;

namespace EmsPortal.Api.Validators.Rems;

/// <summary>
/// Validates a REMS request create payload (WO-111): title and client name are required; at least one
/// of customer email / mobile is required (AC-REMS-004.7); type and priority must be known option-set
/// codes.
/// </summary>
public sealed class CreateRemsRequestRequestValidator : AbstractValidator<CreateRemsRequestRequest>
{
    public CreateRemsRequestRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("title is required.").MaximumLength(200);
        RuleFor(x => x.ClientName).NotEmpty().WithMessage("clientName is required.").MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
        RuleFor(x => x.CustomerEmail).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.CustomerEmail));
        RuleFor(x => x.CustomerMobileNumber).MaximumLength(32).When(x => !string.IsNullOrWhiteSpace(x.CustomerMobileNumber));

        // AC-REMS-004.7: a customer email OR a customer mobile number is required.
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.CustomerEmail) || !string.IsNullOrWhiteSpace(x.CustomerMobileNumber))
            .WithMessage("A customer email or mobile number is required.");

        RuleFor(x => x.Type)
            .Must(RemsRequestOptionCodes.IsKnownType)
            .WithMessage($"type must be one of: {string.Join(", ", RemsRequestOptionCodes.Types)}.");
        RuleFor(x => x.Priority)
            .Must(RemsRequestOptionCodes.IsKnownPriority)
            .WithMessage($"priority must be one of: {string.Join(", ", RemsRequestOptionCodes.Priorities)}.");
    }
}

/// <summary>Validates a REMS request edit payload (WO-111). Supplied fields must be valid; all are optional.</summary>
public sealed class UpdateRemsRequestRequestValidator : AbstractValidator<UpdateRemsRequestRequest>
{
    public UpdateRemsRequestRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200).When(x => x.Title is not null);
        RuleFor(x => x.ClientName).NotEmpty().MaximumLength(200).When(x => x.ClientName is not null);
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
        RuleFor(x => x.CustomerEmail).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.CustomerEmail));
        RuleFor(x => x.CustomerMobileNumber).MaximumLength(32).When(x => !string.IsNullOrWhiteSpace(x.CustomerMobileNumber));
        RuleFor(x => x.Type).Must(RemsRequestOptionCodes.IsKnownType)
            .WithMessage($"type must be one of: {string.Join(", ", RemsRequestOptionCodes.Types)}.")
            .When(x => x.Type is not null);
        RuleFor(x => x.Priority).Must(RemsRequestOptionCodes.IsKnownPriority)
            .WithMessage($"priority must be one of: {string.Join(", ", RemsRequestOptionCodes.Priorities)}.")
            .When(x => x.Priority is not null);

        // Naming an admin and handing the request back to the pool are opposite instructions; sending
        // both leaves the endpoint to guess which one was meant.
        RuleFor(x => x)
            .Must(x => !(x.UnassignAdmin && x.AssignAdminUserId.HasValue))
            .WithMessage("assignAdminUserId and unassignAdmin cannot both be set.");
    }
}

/// <summary>Validates an assign payload (WO-111).</summary>
public sealed class AssignRemsRequestRequestValidator : AbstractValidator<AssignRemsRequestRequest>
{
    public AssignRemsRequestRequestValidator()
    {
        RuleFor(x => x.AdminUserId).NotEmpty().WithMessage("adminUserId is required.");
    }
}

/// <summary>
/// The seeded <c>REMS.Type</c> / <c>REMS.Priority</c> option-set codes (see <c>DefaultOptionSets</c>).
/// Type/priority are trivially closed so they are validated against the known codes; status transitions
/// are driven by the endpoints, not the client.
/// </summary>
internal static class RemsRequestOptionCodes
{
    // From RemsRequestTypes so the accepted set and the codes the controllers branch on cannot drift.
    // 'new_engagement' was merged into 'existing_client' (MergeRemsExistingClientTypes) and is no longer
    // accepted: the migration re-pointed every row that held it, so nothing can still be carrying it.
    public static readonly IReadOnlyList<string> Types = RemsRequestTypes.All;

    public static readonly IReadOnlyList<string> Priorities = new[] { "urgent", "high", "medium", "low" };

    public static bool IsKnownType(string? value) => value is not null && Types.Contains(value);

    public static bool IsKnownPriority(string? value) => value is not null && Priorities.Contains(value);
}
