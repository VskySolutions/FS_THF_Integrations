using System.Globalization;
using IntegrationHub.Application.Abstractions.Connectors;
using IntegrationHub.Application.Abstractions.Connectors.Concur;
using IntegrationHub.Application.Abstractions.Connectors.Maconomy;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Connectors;
using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Application.Concur;

/// <summary>
/// Maps a Concur expense report (header + lines) to the Maconomy schema. Active
/// MappingConfiguration rules override the per-field defaults (fall back to the source
/// value when no rule is configured — AC-COF-002.3).
/// </summary>
public sealed class ConcurExpenseTransformer : TransformerBase<ConcurExpenseReport, MaconomyExpenseReport>
{
    public ConcurExpenseTransformer(IMappingConfigurationRepository mappings, ITransformationRuleEvaluator evaluator)
        : base(mappings, evaluator)
    {
    }

    public override SystemName SourceSystem => SystemName.Concur;

    public override SystemName DestinationSystem => SystemName.Maconomy;

    protected override IReadOnlyDictionary<string, object?> ExtractFields(ConcurExpenseReport source) => new Dictionary<string, object?>
    {
        ["ReportId"] = source.ReportId,
        ["EmployeeId"] = source.EmployeeId,
        ["Status"] = source.Status,
        ["TotalAmount"] = source.TotalAmount,
        ["CurrencyCode"] = source.CurrencyCode,
    };

    protected override MaconomyExpenseReport BuildDestination(IReadOnlyDictionary<string, object?> mapped, ConcurExpenseReport source)
        => new(
            ReportId: Str(mapped, "ReportId") ?? source.ReportId,
            EmployeeId: Str(mapped, "EmployeeId") ?? source.EmployeeId,
            TotalAmount: Dec(mapped, "TotalAmount") ?? source.TotalAmount,
            CurrencyCode: Str(mapped, "CurrencyCode") ?? source.CurrencyCode,
            Lines: source.Lines
                .Select(l => new MaconomyExpenseLine(l.Description ?? l.ExpenseType, l.Amount))
                .ToList());

    internal static string? Str(IReadOnlyDictionary<string, object?> mapped, string key)
        => mapped.TryGetValue(key, out var value) ? value?.ToString() : null;

    internal static decimal? Dec(IReadOnlyDictionary<string, object?> mapped, string key)
        => mapped.TryGetValue(key, out var value) && decimal.TryParse(value?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
            ? d
            : null;
}
