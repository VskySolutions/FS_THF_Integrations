using System.Net.Mail;
using EmsPortal.Api.Models.Rems;
using FluentValidation.Results;

namespace EmsPortal.Api.Validators.Rems;

/// <summary>
/// Validates a <see cref="RemsFormPayloadV1"/> against the FULL industry-group rules (AC-REMS-011.1/2/3,
/// AC-REMS-024.8). Deliberately NOT a FluentValidation <c>AbstractValidator&lt;RemsFormPayloadV1&gt;</c> —
/// the same payload type is bound on the draft endpoint where a partial payload is legal, so this runs
/// only when invoked explicitly (at review AND inside the submit transaction) with the industry group the
/// form was built for. Returns a <see cref="ValidationResult"/> whose failures plug straight into
/// <c>ApiResponseFactory.ValidationError</c>.
/// <para>
/// Required per group: Individual → <c>self</c>; Business → <c>ein</c> + <c>ceo</c>/<c>cfo</c>/
/// <c>accountsPayable</c>; Government → <c>financeDirector</c>. Every entity's physical address and each
/// related entity's business name are required; a required role must carry name, email and phone.
/// </para>
/// </summary>
public sealed class RemsFormPayloadValidator
{
    public const string Individual = "individual";
    public const string Business = "business";
    public const string Government = "government";

    public ValidationResult Validate(RemsFormPayloadV1? payload, string industryGroup)
    {
        var failures = new List<ValidationFailure>();

        if (payload is null)
        {
            failures.Add(new ValidationFailure("payload", "No form data was supplied."));
            return new ValidationResult(failures);
        }

        // ---- Common ----
        if (string.IsNullOrWhiteSpace(payload.ClientName))
        {
            failures.Add(new ValidationFailure("clientName", "Client name is required."));
        }

        // Physical address is always captured for the main entity ("Physical always").
        RequireAddress(failures, "physicalAddress", payload.PhysicalAddress);

        // A separate mailing address is required only when the client says it differs.
        if (payload.MailingDiffers)
        {
            RequireAddress(failures, "mailingAddress", payload.MailingAddress);
        }

        if (!string.IsNullOrWhiteSpace(payload.BillingEmail) && !IsEmail(payload.BillingEmail))
        {
            failures.Add(new ValidationFailure("billingEmail", "Billing email is not a valid email address."));
        }

        // ---- Industry-group role rules ----
        var roles = payload.Roles ?? new RemsRolesPayload();
        switch (industryGroup)
        {
            case Individual:
                RequireRole(failures, "roles.self", roles.Self);
                OptionalRole(failures, "roles.spouse", roles.Spouse);
                break;

            case Business:
                if (string.IsNullOrWhiteSpace(payload.Ein))
                {
                    failures.Add(new ValidationFailure("ein", "EIN is required for a business."));
                }

                RequireRole(failures, "roles.ceo", roles.Ceo);
                RequireRole(failures, "roles.cfo", roles.Cfo);
                RequireRole(failures, "roles.accountsPayable", roles.AccountsPayable);
                OptionalRole(failures, "roles.banker", roles.Banker);
                OptionalRole(failures, "roles.lawyer", roles.Lawyer);
                break;

            case Government:
                RequireRole(failures, "roles.financeDirector", roles.FinanceDirector);
                OptionalRole(failures, "roles.accountsPayable", roles.AccountsPayable);
                break;

            default:
                failures.Add(new ValidationFailure("industryGroup", $"Unsupported industry group '{industryGroup}'."));
                break;
        }

        // ---- Related entities ----
        for (var i = 0; i < payload.RelatedEntities.Count; i++)
        {
            var related = payload.RelatedEntities[i];
            if (string.IsNullOrWhiteSpace(related.BusinessName))
            {
                failures.Add(new ValidationFailure(
                    $"relatedEntities[{i}].businessName", "Business name is required for each related entity."));
            }
        }

        return new ValidationResult(failures);
    }

    /// <summary>
    /// A required address must carry the whole standard block except line 2: country, state, city,
    /// address line 1 and zip code. The property names are the payload's own (street = address line 1).
    /// </summary>
    private static void RequireAddress(List<ValidationFailure> failures, string prefix, RemsAddressPayload? address)
    {
        if (address is null)
        {
            failures.Add(new ValidationFailure(
                prefix, "A complete address is required (country, state, city, address line 1, zip code)."));
            return;
        }

        // Country is keyed on the ISO code, not the display name — that is what the client's cascade binds
        // and what the state / city lists below it are resolved from.
        RequireField(failures, $"{prefix}.countryCode", address.CountryCode, "Country is required.");
        RequireField(failures, $"{prefix}.state", address.State, "State / Province is required.");
        RequireField(failures, $"{prefix}.city", address.City, "City is required.");
        RequireField(failures, $"{prefix}.street", address.Street, "Address Line 1 is required.");
        RequireField(failures, $"{prefix}.zip", address.Zip, "Zip Code is required.");
    }

    /// <summary>A required role must carry a name, a valid email and a phone.</summary>
    private static void RequireRole(List<ValidationFailure> failures, string prefix, RemsRolePayload? role)
    {
        if (role is null || !role.HasAny)
        {
            failures.Add(new ValidationFailure(prefix, "This contact is required (name, email, phone)."));
            return;
        }

        ValidateRoleFields(failures, prefix, role);
    }

    /// <summary>An optional role is unvalidated when omitted, but must be complete once the client starts filling it in.</summary>
    private static void OptionalRole(List<ValidationFailure> failures, string prefix, RemsRolePayload? role)
    {
        if (role is not null && role.HasAny)
        {
            ValidateRoleFields(failures, prefix, role);
        }
    }

    private static void ValidateRoleFields(List<ValidationFailure> failures, string prefix, RemsRolePayload role)
    {
        RequireField(failures, $"{prefix}.name", role.Name, "Name is required.");
        RequireField(failures, $"{prefix}.phone", role.Phone, "Phone is required.");

        if (string.IsNullOrWhiteSpace(role.Email))
        {
            failures.Add(new ValidationFailure($"{prefix}.email", "Email is required."));
        }
        else if (!IsEmail(role.Email))
        {
            failures.Add(new ValidationFailure($"{prefix}.email", "Email is not a valid email address."));
        }
    }

    private static void RequireField(List<ValidationFailure> failures, string property, string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add(new ValidationFailure(property, message));
        }
    }

    private static bool IsEmail(string value) => MailAddress.TryCreate(value.Trim(), out _);
}
