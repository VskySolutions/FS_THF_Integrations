using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Api.Integrations;

/// <summary>
/// Canonical catalogue of the flows (interfaces) available for a (source → target) system pair.
/// A mapping template is scoped to one of these, and the Jobs trigger derives the flow from the
/// selected template. Interface names match <c>IntegrationJob.InterfaceName</c>.
/// </summary>
public static class IntegrationFlows
{
    public const string ExpenseImport = "ExpenseImport";
    public const string InvoiceImport = "InvoiceImport";
    public const string VendorPaymentImport = "VendorPaymentImport";

    /// <summary>Customer onboarding → Maconomy customer master sync (Platform → Maconomy).</summary>
    public const string CustomerSync = "CustomerSync";

    /// <summary>Interface name → human label, for the Concur → Maconomy inbound flows.</summary>
    private static readonly IReadOnlyDictionary<string, string> ConcurToMaconomy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [ExpenseImport] = "Expense Reports",
        [InvoiceImport] = "Vendor Invoices",
        [VendorPaymentImport] = "Vendor Payments",
    };

    /// <summary>Interface name → human label, for the Platform → Maconomy outbound flows.</summary>
    private static readonly IReadOnlyDictionary<string, string> PlatformToMaconomy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [CustomerSync] = "Customer Sync",
    };

    /// <summary>The valid flows for a pair (empty when the pair has no defined flows yet).</summary>
    public static IReadOnlyDictionary<string, string> For(SystemName source, SystemName target)
        => (source, target) switch
        {
            (SystemName.Concur, SystemName.Maconomy) => ConcurToMaconomy,
            (SystemName.Platform, SystemName.Maconomy) => PlatformToMaconomy,
            _ => new Dictionary<string, string>(),
        };

    /// <summary>True when <paramref name="interfaceName"/> is a valid flow for the pair.</summary>
    public static bool IsValid(SystemName source, SystemName target, string interfaceName)
        => For(source, target).ContainsKey(interfaceName);

    /// <summary>Human label for a flow, or the raw interface name when unknown.</summary>
    public static string Label(SystemName source, SystemName target, string interfaceName)
        => For(source, target).TryGetValue(interfaceName, out var label) ? label : interfaceName;

    /// <summary>Every defined flow across all pairs (drives the mapping list and validation).</summary>
    public static IReadOnlyList<FlowDescriptor> All()
        => ConcurToMaconomy.Select(kv => new FlowDescriptor(kv.Key, kv.Value, SystemName.Concur, SystemName.Maconomy))
            .Concat(PlatformToMaconomy.Select(kv => new FlowDescriptor(kv.Key, kv.Value, SystemName.Platform, SystemName.Maconomy)))
            .ToList();

    /// <summary>Resolves the (source, target) pair a flow runs on; false when the flow is unknown.</summary>
    public static bool TryGetPair(string interfaceName, out SystemName source, out SystemName target)
    {
        if (ConcurToMaconomy.ContainsKey(interfaceName))
        {
            source = SystemName.Concur;
            target = SystemName.Maconomy;
            return true;
        }
        if (PlatformToMaconomy.ContainsKey(interfaceName))
        {
            source = SystemName.Platform;
            target = SystemName.Maconomy;
            return true;
        }
        source = default;
        target = default;
        return false;
    }
}

/// <summary>A flow: its interface name, label, and the systems it maps between.</summary>
public sealed record FlowDescriptor(string InterfaceName, string Label, SystemName Source, SystemName Target);
