using IntegrationHub.Application.Abstractions.Connectors;
using IntegrationHub.Application.Abstractions.Connectors.Concur;
using IntegrationHub.Application.Abstractions.Connectors.Maconomy;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Connectors;
using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Application.Concur;

/// <summary>Maps a Concur vendor payment to the Maconomy schema.</summary>
public sealed class ConcurPaymentTransformer : TransformerBase<ConcurVendorPayment, MaconomyVendorPayment>
{
    public ConcurPaymentTransformer(IMappingConfigurationRepository mappings, ITransformationRuleEvaluator evaluator)
        : base(mappings, evaluator)
    {
    }

    public override SystemName SourceSystem => SystemName.Concur;

    public override SystemName DestinationSystem => SystemName.Maconomy;

    protected override IReadOnlyDictionary<string, object?> ExtractFields(ConcurVendorPayment source) => new Dictionary<string, object?>
    {
        ["PaymentId"] = source.PaymentId,
        ["InvoiceId"] = source.InvoiceId,
        ["Amount"] = source.Amount,
    };

    protected override MaconomyVendorPayment BuildDestination(IReadOnlyDictionary<string, object?> mapped, ConcurVendorPayment source)
        => new(
            PaymentId: ConcurExpenseTransformer.Str(mapped, "PaymentId") ?? source.PaymentId,
            InvoiceNumber: ConcurExpenseTransformer.Str(mapped, "InvoiceId") ?? source.InvoiceId,
            Amount: ConcurExpenseTransformer.Dec(mapped, "Amount") ?? source.Amount);
}
