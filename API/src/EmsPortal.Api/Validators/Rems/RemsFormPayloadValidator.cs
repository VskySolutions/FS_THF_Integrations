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
/// <c>primaryContact</c>/<c>financialContact</c>; Government → <c>financeDirector</c>. The physical and
/// mailing addresses are both required, as are each related entity's name and email address; a required
/// role must carry a first name, a last name and a valid email. Billing addresses are optional in full,
/// but one that has been started must carry a whole address, and a valid email where it gives one.
/// </para>
/// </summary>
public sealed class RemsFormPayloadValidator
{
    public const string Individual = "individual";
    public const string Government = "government";

    /// <summary>
    /// How many places a client may be invoiced at. Not a limit anybody should meet — it is the guard
    /// against a stuck key or a hand-written request filing four hundred addresses against one entity.
    /// Mirrors the browser's <c>MAX_BILLING_ADDRESSES</c>.
    /// </summary>
    public const int MaxBillingAddresses = 10;

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
            // …and what IS typed has to read as a name. An individual client becomes a Person record under
            // exactly these two boxes. See PersonNames, which the browser mirrors.
            RequireName(failures, "clientFirstName", payload.ClientFirstName, "First name");
            RequireName(failures, "clientLastName", payload.ClientLastName, "Last name");
        }
        else if (string.IsNullOrWhiteSpace(payload.ClientName))
        {
            failures.Add(new ValidationFailure("clientName", "Client name is required."));
        }

        // Both are required, unconditionally. The form offers a "copy from" button rather than a "same as"
        // flag, so there is no flag to read and every client fills both in — which the browser enforces too.
        RequireAddress(failures, "physicalAddress", payload.PhysicalAddress);
        RequireAddress(failures, "mailingAddress", payload.MailingAddress);

        // Where invoices go, and who each one is addressed to. Every row is optional — a client who gives
        // none is invoiced at their mailing address — but a row somebody has STARTED has to be finished:
        // an invoice addressed to half an address reaches nobody. The addressee's NAME is not required
        // even then, because plenty of clients are invoiced at a department rather than at a person.
        //
        // `billingEmail` — the retired two-box billing contact's address — is deliberately no longer
        // checked. The form stopped asking for it, so a message about it would point at a box nobody can
        // see, and the only payloads still carrying one are the record of a form that is gone.
        if (payload.BillingAddresses.Count > MaxBillingAddresses)
        {
            failures.Add(new ValidationFailure(
                "billingAddresses", $"Give at most {MaxBillingAddresses} billing addresses."));
        }

        for (var i = 0; i < payload.BillingAddresses.Count; i++)
        {
            ValidateBillingAddress(failures, $"billingAddresses[{i}]", payload.BillingAddresses[i]);
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
            OptionalRole(failures, "roles.otherContact", roles.OtherContact);
        }
        else if (industryGroup == Government)
        {
            RequireRole(failures, "roles.financeDirector", roles.FinanceDirector);
            OptionalRole(failures, "roles.otherContact", roles.OtherContact);
        }
        else
        {
            failures.Add(new ValidationFailure("industryGroup", $"Unsupported entity type '{industryGroup}'."));
        }

        // ---- Billing contacts ----
        // Neither `roles.billingContact` nor `additionalBillingContacts` is validated any more. The
        // Billing Contact block is gone from the form — whoever an invoice is addressed to travels ON the
        // billing address now — so a half-finished answer to a question nobody is asked would block a
        // client on a box their form does not show. The only payloads still carrying one are drafts
        // started before the change; they are read, materialised and shown exactly as they always were.

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

    /// <summary>
    /// A billing address the client has started. It is a place AND a person, and either half on its own
    /// is a legitimate answer — an invoice can be addressed to "Accounts Payable" at a street, or emailed
    /// to a named person with no street at all — so what is enforced is that whichever half they began is
    /// complete: one postal line typed means the whole postal block. A row with nothing in it is not an
    /// answer and never reaches here.
    /// </summary>
    private static void ValidateBillingAddress(
        List<ValidationFailure> failures, string prefix, RemsAddressPayload? address)
    {
        if (address is null || !address.HasAnyContent)
        {
            return;
        }

        if (address.HasAny)
        {
            RequireAddress(failures, prefix, address);
        }

        if (!string.IsNullOrWhiteSpace(address.Email) && !IsEmail(address.Email))
        {
            failures.Add(new ValidationFailure($"{prefix}.email", "Email is not a valid email address."));
        }

        // Whatever HAS been typed into the two name boxes has to read as a name. The addressee is never
        // required, but it is printed on an invoice.
        RequireName(failures, $"{prefix}.firstName", address.FirstName, "First name");
        RequireName(failures, $"{prefix}.lastName", address.LastName, "Last name");
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
            RequireName(failures, $"{prefix}.firstName", role.FirstName, "First name");
            RequireName(failures, $"{prefix}.lastName", role.LastName, "Last name");
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

    /// <summary>
    /// The value must read as a person's name where one was given. Silent on an empty value — the
    /// <see cref="RequireField"/> beside it is what says the box has to be filled in, and reporting both
    /// against one field would show the client two messages about one empty box.
    /// </summary>
    private static void RequireName(List<ValidationFailure> failures, string property, string? value, string label)
    {
        if (PersonNames.Issue(value, label) is { } issue)
        {
            failures.Add(new ValidationFailure(property, issue));
        }
    }

    private static bool IsEmail(string value) => MailAddress.TryCreate(value.Trim(), out _);
}
