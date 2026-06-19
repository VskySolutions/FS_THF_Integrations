using System.Globalization;
using IntegrationHub.Application.Abstractions.Connectors;
using IntegrationHub.Application.Abstractions.Connectors.Maconomy;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Application.Customers;

/// <summary>
/// Builds the Maconomy customer payload from a <see cref="CustomerRequest"/>, applying the tenant's
/// active <see cref="MappingConfiguration"/> field rules for the <see cref="InterfaceName"/> flow
/// (Platform → Maconomy) — the same per-tenant mapping mechanism used by the Concur import flows.
/// Any Maconomy target field not covered by a rule falls back to the platform default mapping, so
/// the sync works out of the box and admins can override individual fields via the Flow Mappings UI.
/// </summary>
public interface ICustomerMaconomyMapper
{
    Task<MaconomyCustomer> BuildAsync(CustomerRequest request, CancellationToken cancellationToken = default);
}

public sealed class CustomerMaconomyMapper : ICustomerMaconomyMapper
{
    /// <summary>Flow interface name (must match <c>IntegrationFlows.CustomerSync</c>).</summary>
    public const string InterfaceName = "CustomerSync";

    /// <summary>Default Maconomy target field → CustomerRequest source field (the out-of-the-box mapping).</summary>
    private static readonly IReadOnlyDictionary<string, string> DefaultMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Name"] = nameof(CustomerRequest.CompanyName),
        ["LegalName"] = nameof(CustomerRequest.LegalName),
        ["ContactPerson"] = nameof(CustomerRequest.ContactPerson),
        ["Email"] = nameof(CustomerRequest.EmailAddress),
        ["Phone"] = nameof(CustomerRequest.PhoneNumber),
        ["Website"] = nameof(CustomerRequest.Website),
        // Address fields now live on the linked Address record; the source-field keys are kept stable
        // (string literals) so existing tenant MappingConfiguration rules continue to resolve.
        ["Country"] = "Country",
        ["State"] = "StateProvince",
        ["City"] = "City",
        ["AddressLine1"] = "AddressLine1",
        ["AddressLine2"] = "AddressLine2",
        ["PostalCode"] = "PostalCode",
        ["TaxNumber"] = nameof(CustomerRequest.TaxNumber),
        ["RegistrationNumber"] = nameof(CustomerRequest.RegistrationNumber),
        ["BusinessUnit"] = nameof(CustomerRequest.BusinessUnit),
        ["Currency"] = nameof(CustomerRequest.Currency),
        ["CustomerGroup"] = nameof(CustomerRequest.CustomerGroup),
        ["PaymentTerms"] = nameof(CustomerRequest.PaymentTerms),
        ["CreditLimit"] = nameof(CustomerRequest.CreditLimit),
        ["Industry"] = nameof(CustomerRequest.Industry),
        ["InvoiceLanguage"] = nameof(CustomerRequest.InvoiceLanguage),
        ["BillingEmail"] = nameof(CustomerRequest.BillingEmail),
    };

    private readonly IMappingConfigurationRepository _mappings;
    private readonly ITransformationRuleEvaluator _ruleEvaluator;

    public CustomerMaconomyMapper(IMappingConfigurationRepository mappings, ITransformationRuleEvaluator ruleEvaluator)
    {
        _mappings = mappings;
        _ruleEvaluator = ruleEvaluator;
    }

    public async Task<MaconomyCustomer> BuildAsync(CustomerRequest request, CancellationToken cancellationToken = default)
    {
        var source = SourceFields(request);
        var target = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        // 1) Apply the tenant's active field rules for this flow (resolved against the ambient tenant).
        var rules = await _mappings.GetActiveForFlowAsync(SystemName.Platform, SystemName.Maconomy, InterfaceName, cancellationToken);
        foreach (var rule in rules)
        {
            source.TryGetValue(rule.SourceField, out var value);
            target[rule.DestinationField] = _ruleEvaluator.Evaluate(rule.TransformationRule, value, source);
        }

        // 2) Fill any target field not set by a rule from the platform default mapping.
        foreach (var (destField, srcField) in DefaultMap)
        {
            if (!target.ContainsKey(destField) && source.TryGetValue(srcField, out var value))
            {
                target[destField] = value;
            }
        }

        return new MaconomyCustomer(
            Name: Str(target, "Name") ?? string.Empty,
            LegalName: Str(target, "LegalName") ?? string.Empty,
            ContactPerson: Str(target, "ContactPerson"),
            Email: Str(target, "Email"),
            Phone: Str(target, "Phone"),
            Website: Str(target, "Website"),
            Country: Str(target, "Country") ?? string.Empty,
            State: Str(target, "State"),
            City: Str(target, "City"),
            AddressLine1: Str(target, "AddressLine1") ?? string.Empty,
            AddressLine2: Str(target, "AddressLine2"),
            PostalCode: Str(target, "PostalCode"),
            TaxNumber: Str(target, "TaxNumber"),
            RegistrationNumber: Str(target, "RegistrationNumber"),
            BusinessUnit: Str(target, "BusinessUnit"),
            Currency: Str(target, "Currency"),
            CustomerGroup: Str(target, "CustomerGroup"),
            PaymentTerms: Str(target, "PaymentTerms"),
            CreditLimit: Dec(target, "CreditLimit"),
            Industry: Str(target, "Industry"),
            InvoiceLanguage: Str(target, "InvoiceLanguage"),
            BillingEmail: Str(target, "BillingEmail"));
    }

    private static Dictionary<string, object?> SourceFields(CustomerRequest r) => new(StringComparer.OrdinalIgnoreCase)
    {
        [nameof(r.LegalName)] = r.LegalName,
        [nameof(r.CompanyName)] = r.CompanyName,
        [nameof(r.ContactPerson)] = r.ContactPerson,
        [nameof(r.EmailAddress)] = r.EmailAddress,
        [nameof(r.PhoneNumber)] = r.PhoneNumber,
        [nameof(r.Website)] = r.Website,
        // Address values are read from the linked shared Address record.
        ["Country"] = r.Address?.CountryName,
        ["StateProvince"] = r.Address?.StateName,
        ["City"] = r.Address?.CityName,
        ["AddressLine1"] = r.Address?.AddressLine1,
        ["AddressLine2"] = r.Address?.AddressLine2,
        ["PostalCode"] = r.Address?.PostalCode,
        [nameof(r.TaxNumber)] = r.TaxNumber,
        [nameof(r.RegistrationNumber)] = r.RegistrationNumber,
        [nameof(r.BusinessUnit)] = r.BusinessUnit,
        [nameof(r.Currency)] = r.Currency,
        [nameof(r.CustomerGroup)] = r.CustomerGroup,
        [nameof(r.PaymentTerms)] = r.PaymentTerms,
        [nameof(r.CreditLimit)] = r.CreditLimit,
        [nameof(r.Industry)] = r.Industry,
        [nameof(r.InvoiceLanguage)] = r.InvoiceLanguage,
        [nameof(r.BillingEmail)] = r.BillingEmail,
    };

    private static string? Str(IReadOnlyDictionary<string, object?> target, string key)
    {
        if (!target.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }
        var s = value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    private static decimal? Dec(IReadOnlyDictionary<string, object?> target, string key)
    {
        if (!target.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }
        return value switch
        {
            decimal d => d,
            double db => (decimal)db,
            int i => i,
            _ => decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : null,
        };
    }
}
