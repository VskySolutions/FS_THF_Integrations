using IntegrationHub.Application.Abstractions.Connectors;
using IntegrationHub.Application.Abstractions.Connectors.Concur;
using IntegrationHub.Application.Abstractions.Connectors.Maconomy;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Connectors;
using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Application.Concur;

/// <summary>Maps a Concur vendor invoice (header + lines) to the Maconomy schema.</summary>
public sealed class ConcurInvoiceTransformer : TransformerBase<ConcurVendorInvoice, MaconomyVendorInvoice>
{
    public ConcurInvoiceTransformer(IMappingConfigurationRepository mappings, ITransformationRuleEvaluator evaluator)
        : base(mappings, evaluator)
    {
    }

    public override SystemName SourceSystem => SystemName.Concur;

    public override SystemName DestinationSystem => SystemName.Maconomy;

    protected override IReadOnlyDictionary<string, object?> ExtractFields(ConcurVendorInvoice source) => new Dictionary<string, object?>
    {
        ["InvoiceNumber"] = source.InvoiceNumber,
        ["VendorId"] = source.VendorId,
        ["Amount"] = source.Amount,
        ["CurrencyCode"] = source.CurrencyCode,
    };

    protected override MaconomyVendorInvoice BuildDestination(IReadOnlyDictionary<string, object?> mapped, ConcurVendorInvoice source)
        => new(
            InvoiceNumber: ConcurExpenseTransformer.Str(mapped, "InvoiceNumber") ?? source.InvoiceNumber,
            VendorId: ConcurExpenseTransformer.Str(mapped, "VendorId") ?? source.VendorId,
            Amount: ConcurExpenseTransformer.Dec(mapped, "Amount") ?? source.Amount,
            CurrencyCode: ConcurExpenseTransformer.Str(mapped, "CurrencyCode") ?? source.CurrencyCode,
            Lines: source.Lines.Select(l => new MaconomyVendorInvoiceLine(l.Description, l.Amount)).ToList());
}
