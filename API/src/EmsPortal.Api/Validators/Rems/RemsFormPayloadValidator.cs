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
/// Required per group: Individual → a first and last name + <c>self</c>; Business → <c>ein</c> +
/// <c>primaryContact</c>/<c>financialContact</c>/<c>billingContact</c>; Government →
/// <c>financeDirector</c>. The physical and mailing addresses are both required, as are each related
/// entity's name and email address; a required role must carry a first name, a last name and a valid email.
/// </para>
/// </summary>
public sealed class RemsFormPayloadValidator
{
    public const string Individual = "individual";
    public const string Government = "government";

    // The business FAMILY: the kinds of business THF onboards are asked for exactly the same things, so
    // what separates them is what the client IS, not what the form asks.
    //
    // `Business` is not offered in the picker but is STILL RECOGNISED here: forms sent before the split
    // into three carry the code, and a client part-way through one must be able to finish.
    public const string Business = "business";
    public const string NotForProfit = "not_for_profit";
    public const string Insurance = "insurance";
    public const string Commercial = "commercial";

    /// <summary>
    /// A trust or a decedent's estate. In the business family because it is asked the same things: it has
    /// an EIN of its own and is acted for by trustees or personal representatives, so the primary,
    /// financial and billing contacts are the people who act for it. It is emphatically not an individual
    /// — treating it as one files the trust's affairs under its trustee's own name.
    /// </summary>
    public const string TrustEstate = "trust_estate";

    private static readonly HashSet<string> BusinessGroups =
        new(StringComparer.Ordinal) { Business, NotForProfit, Insurance, Commercial, TrustEstate };

    /// <summary>True for any industry group that asks the business questions.</summary>
    public static bool IsBusinessGroup(string? industryGroup)
        => industryGroup is not null && BusinessGroups.Contains(industryGroup);

    public ValidationResult Validate(RemsFormPayloadV1? payload, string industryGroup)
    {
        var failures = new List<ValidationFailure>();

        if (payload is null)
        {
            failures.Add(new ValidationFailure("payload", "No form data was supplied."));
            return new ValidationResult(failures);
        }

        // ---- Common ----
        // An individual is a person and is asked for a first and a last name; a business or government
        // body is asked for the one name it has. Both land in ClientName — see EffectiveClientName — but
        // what has to be FILLED IN differs, and pointing at "clientName" on a form showing two boxes would
        // highlight neither of them.
        if (industryGroup == Individual)
        {
            RequireField(failures, "clientFirstName", payload.ClientFirstName, "First name is required.");
            RequireField(failures, "clientLastName", payload.ClientLastName, "Last name is required.");
        }
        else if (string.IsNullOrWhiteSpace(payload.ClientName))
        {
            failures.Add(new ValidationFailure("clientName", "Client name is required."));
        }

        // Both are required, unconditionally. The form offers a "copy from" button rather than a "same as"
        // flag, so there is no flag to read and every client fills both in — which the browser enforces too.
        RequireAddress(failures, "physicalAddress", payload.PhysicalAddress);
        RequireAddress(failures, "mailingAddress", payload.MailingAddress);

        if (!string.IsNullOrWhiteSpace(payload.BillingEmail) && !IsEmail(payload.BillingEmail))
        {
            failures.Add(new ValidationFailure("billingEmail", "Billing email is not a valid email address."));
        }

        // Optional like the rest of the spouse block, but checked when given — a mistyped address is
        // worse than a blank one, since nobody finds out until someone tries to use it.
        if (!string.IsNullOrWhiteSpace(payload.SpouseEmail) && !IsEmail(payload.SpouseEmail))
        {
            failures.Add(new ValidationFailure("spouseEmail", "Spouse email is not a valid email address."));
        }

        // ---- Industry-group role rules ----
        // Normalized, so a payload written before the business roles were renamed is validated on the
        // answers it actually carries rather than failing for three contacts it gave under the old keys.
        var roles = payload.EffectiveRoles;
        // if/else rather than a switch: the business branch matches a FAMILY of codes, not one literal.
        if (industryGroup == Individual)
        {
            RequireRole(failures, "roles.self", roles.Self);
            OptionalRole(failures, "roles.spouse", roles.Spouse);
        }
        else if (IsBusinessGroup(industryGroup))
        {
            if (string.IsNullOrWhiteSpace(payload.Ein))
            {
                failures.Add(new ValidationFailure("ein", "EIN is required for a business."));
            }

            RequireRole(failures, "roles.primaryContact", roles.PrimaryContact);
            RequireRole(failures, "roles.financialContact", roles.FinancialContact);
            RequireRole(failures, "roles.billingContact", roles.BillingContact);
            OptionalRole(failures, "roles.otherContact", roles.OtherContact);
        }
        else if (industryGroup == Government)
        {
            RequireRole(failures, "roles.financeDirector", roles.FinanceDirector);
            OptionalRole(failures, "roles.billingContact", roles.BillingContact);
            OptionalRole(failures, "roles.otherContact", roles.OtherContact);
        }
        else
        {
            failures.Add(new ValidationFailure("industryGroup", $"Unsupported entity type '{industryGroup}'."));
        }

        // ---- Additional entities ----
        // Each row is another of the client's businesses for the firm to set up separately, so it needs a
        // name to file it under and an email to reach it on — the request this row exists to prompt is
        // opened by emailing an intake form, so a row without an address becomes nothing. Phone optional.
        for (var i = 0; i < payload.RelatedEntities.Count; i++)
        {
            var related = payload.RelatedEntities[i];
            RequireField(
                failures, $"relatedEntities[{i}].fullName", related.FullName,
                "A client / entity name is required for each additional entity.");

            if (string.IsNullOrWhiteSpace(related.EmailAddress))
            {
                failures.Add(new ValidationFailure(
                    $"relatedEntities[{i}].emailAddress", "An email address is required for each additional entity."));
            }
            else if (!IsEmail(related.EmailAddress))
            {
                failures.Add(new ValidationFailure(
                    $"relatedEntities[{i}].emailAddress", "Email is not a valid email address."));
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

    /// <summary>A required role must carry a first name, a last name and a valid email; the phone is optional.</summary>
    private static void RequireRole(List<ValidationFailure> failures, string prefix, RemsRolePayload? role)
    {
        if (role is null || !role.HasAny)
        {
            failures.Add(new ValidationFailure(prefix, "This contact is required (name and email)."));
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

    /// <summary>
    /// A contact is a first name, a last name and a valid email. The phone is optional — captured when
    /// known, never a reason to block the form.
    /// <para>
    /// A payload written before the name was split into two carries only <c>name</c>. It is accepted as
    /// it stands: the client filled that form in good faith, and re-opening it would ask them to retype a
    /// name they already gave. Anything typed into the two boxes since is validated as two boxes.
    /// </para>
    /// </summary>
    private static void ValidateRoleFields(List<ValidationFailure> failures, string prefix, RemsRolePayload role)
    {
        var preSplit = string.IsNullOrWhiteSpace(role.FirstName)
            && string.IsNullOrWhiteSpace(role.LastName)
            && !string.IsNullOrWhiteSpace(role.Name);
        if (!preSplit)
        {
            RequireField(failures, $"{prefix}.firstName", role.FirstName, "First name is required.");
            RequireField(failures, $"{prefix}.lastName", role.LastName, "Last name is required.");
        }

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
