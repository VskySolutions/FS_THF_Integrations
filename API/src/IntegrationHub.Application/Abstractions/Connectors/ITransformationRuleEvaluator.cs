namespace IntegrationHub.Application.Abstractions.Connectors;

/// <summary>
/// Applies a single <c>MappingConfiguration.TransformationRule</c> expression to a source
/// field value (AC-COF-005.3). Supported rule kinds: date format conversion, code lookups,
/// string concatenation, and value mapping. A null/empty rule passes the value through.
/// </summary>
public interface ITransformationRuleEvaluator
{
    /// <summary>
    /// Evaluates <paramref name="rule"/> against <paramref name="sourceValue"/>.
    /// <paramref name="sourceFields"/> gives access to sibling fields for rules like concat.
    /// </summary>
    object? Evaluate(string? rule, object? sourceValue, IReadOnlyDictionary<string, object?> sourceFields);
}
