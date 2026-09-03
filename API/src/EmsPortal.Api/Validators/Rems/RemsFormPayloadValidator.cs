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
/// Required per group: Individual → a first and last name (no contact roles at all); Business →
/// <c>ein</c> + <c>primaryContact</c>/<c>financialContact</c>; Government → <c>financeDirector</c>. The
/// physical address is always required and the mailing one unless the client said it is the same; a
/// required role must carry a first name, a last name and a valid email. At least one billing block is
/// required, and every block that carries anything must be whole — a name, a valid email address and a
/// complete address. Related entities are asked of everyone EXCEPT an individual, and each one needs a
/// name and an email address.
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

    /// <summary>
    /// How many other people one individual client may declare on their return. The same kind of guard
    /// <see cref="MaxBillingAddresses"/> is — against a stuck key, not against a real family. Mirrors the
    /// browser's <c>MAX_ADDITIONAL_INDIVIDUALS</c>.
    /// </summary>
    public const int MaxAdditionalIndividuals = 10;

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

        // The physical address always. The mailing one only where the client said it differs — ticking
        // "same as physical" IS giving us a mailing address, and EffectiveMailingAddress is the one node
        // every reader downstream stages and shows.
        RequireAddress(failures, "physicalAddress", payload.PhysicalAddress);
        if (!payload.MailingSameAsPhysical)
        {
            RequireAddress(failures, "mailingAddress", payload.MailingAddress);
        }

        // Billing: who each invoice is for, and where it goes. REQUIRED, and required in full — the firm
        // bills somebody, and "whoever the post goes to, addressed to whoever is on the request" was a
        // guess the form used to make on the client's behalf. A second block is the client's to add, and
        // is held to exactly the same standard.
        //
        // `billingEmail` — the retired two-box billing contact's address — is deliberately not checked.
        // The form stopped asking for it, so a message about it would point at a box nobody can see, and
        // the only payloads still carrying one are the record of a form that is gone.
        if (payload.BillingAddresses.Count > MaxBillingAddresses)
        {
            failures.Add(new ValidationFailure(
                "billingAddresses", $"Give at most {MaxBillingAddresses} billing blocks."));
        }

        if (payload.EffectiveBillingAddresses.Count == 0)
        {
            failures.Add(new ValidationFailure(
                "billingAddresses",
                "Billing information is required — give a name, an email address and an address for the invoice."));
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
            // No contact roles at all. "Self" was the client re-typing the name, email and phone the first
            // card had just asked them for, and "Spouse" asked for a name where what the firm needs to
            // know about a second person on a return is how it is filed and who pays for it — which the
            // Spouse & More Individuals card asks, and which fits children too. A submission that carries
            // either is still read, staged and shown; nothing on the form produces one any more.
            //
            // Everyone else on the return, with the type-driven rules the browser also enforces.
            if (payload.AdditionalIndividuals.Count > MaxAdditionalIndividuals)
            {
                failures.Add(new ValidationFailure(
                    "additionalIndividuals", $"Give at most {MaxAdditionalIndividuals} people here."));
            }

            for (var i = 0; i < payload.AdditionalIndividuals.Count; i++)
            {
                var individual = payload.AdditionalIndividuals[i];
                // A block somebody opened and left empty is a change of mind, not an answer — the browser
                // drops those on the way out, and one that arrives anyway is not worth nine complaints.
                if (individual is not { HasAny: true })
                {
                    continue;
                }

                ValidateAdditionalIndividual(failures, $"additionalIndividuals[{i}]", individual);
            }
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
        //
        // Not asked of an INDIVIDUAL, and so not checked for one: a person is not a holding structure, and
        // the card is off their form. A payload that carries a row from before it was dropped is still
        // read and still materialised — a complaint about it would point at a box nobody can see.
        if (industryGroup != Individual)
        {
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
    /// A billing block the client has started. It is a place AND a person, and the form asks for both:
    /// an invoice needs somewhere to go and a name to be addressed to, and the two used to be optional
    /// separately, which produced rows that were half an answer.
    /// <para>
    /// A block with nothing in it at all is not an answer and is skipped — the "at least one" rule above
    /// is what catches a client who left every one of them blank, and complaining about nine fields on an
    /// empty second block would bury it.
    /// </para>
    /// </summary>
    private static void ValidateBillingAddress(
        List<ValidationFailure> failures, string prefix, RemsAddressPayload? address)
    {
        if (address is null || !address.HasAnyContent)
        {
            return;
        }

        RequireAddress(failures, prefix, address);

        RequireField(failures, $"{prefix}.firstName", address.FirstName, "First name is required.");
        RequireField(failures, $"{prefix}.lastName", address.LastName, "Last name is required.");
        RequireName(failures, $"{prefix}.firstName", address.FirstName, "First name");
        RequireName(failures, $"{prefix}.lastName", address.LastName, "Last name");

        if (string.IsNullOrWhiteSpace(address.Email))
        {
            failures.Add(new ValidationFailure($"{prefix}.email", "Email Address is required."));
        }
        else if (!IsEmail(address.Email))
        {
            failures.Add(new ValidationFailure($"{prefix}.email", "Email is not a valid email address."));
        }
    }

    /// <summary>
    /// One of the other people on an individual's return. Everything is required except the phone: the
    /// type, the filing type, both names, a valid email address and the billing preference. Billing NAMES
    /// are NOT required — the form stopped asking for them — but any that arrive are still shape-checked.
    /// <para>
    /// The TYPE is required because everything else about the row is read against it: it decides the
    /// filing type, whether the minor question exists, and who is invoiced.
    /// </para>
    /// <para>
    /// The filing type and the billing preference are checked on the RAW fields rather than through their
    /// Effective* readings, which fall back to joint and to the primary client and so can never be blank.
    /// Falling back is the right behaviour for materialising a row; it is the wrong behaviour for saying
    /// whether the client answered, and this is the one place that question is being asked.
    /// </para>
    /// </summary>
    private static void ValidateAdditionalIndividual(
        List<ValidationFailure> failures, string prefix, RemsAdditionalIndividualPayload individual)
    {
        RequireField(failures, $"{prefix}.type", individual.Type, "A type is required (spouse, child or other).");
        RequireField(
            failures, $"{prefix}.filingType", individual.FilingType,
            "A filing type is required (joint or individual).");
        RequireField(failures, $"{prefix}.firstName", individual.FirstName, "First name is required.");
        RequireField(failures, $"{prefix}.lastName", individual.LastName, "Last name is required.");
        RequireName(failures, $"{prefix}.firstName", individual.FirstName, "First name");
        RequireName(failures, $"{prefix}.lastName", individual.LastName, "Last name");
        RequireField(
            failures, $"{prefix}.billingPreference", individual.BillingPreference,
            "A billing preference is required.");

        if (string.IsNullOrWhiteSpace(individual.Email))
        {
            failures.Add(new ValidationFailure($"{prefix}.email", "Email Address is required."));
        }
        else if (!IsEmail(individual.Email))
        {
            failures.Add(new ValidationFailure($"{prefix}.email", "Email is not a valid email address."));
        }

        // Billing NAMES are no longer required, because the form no longer asks for them: the invoice for
        // this person's return is addressed to this person, and asking the client to type their own
        // child's name a second time to say so was asking them to repeat themselves.
        //
        // Still SHAPE-checked. The columns remain, older submissions carry values, and a client editing a
        // draft raised before the change must not be able to save a phone number into a name box just
        // because nothing insists the box is filled.
        RequireName(failures, $"{prefix}.billingFirstName", individual.BillingFirstName, "Billing first name");
        RequireName(failures, $"{prefix}.billingLastName", individual.BillingLastName, "Billing last name");
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
