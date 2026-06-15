using IntegrationHub.Application.Abstractions.Connectors;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Shared.Connectors;

namespace IntegrationHub.Application.Connectors;

/// <summary>
/// Base class for transformers. On each invocation it reads the active
/// <c>MappingConfiguration</c> rows for the (source, destination) pair + flow (fresh, no
/// caching), applies each rule via <see cref="ITransformationRuleEvaluator"/>, and projects
/// the result. Missing optional fields are omitted; missing required fields produce a
/// <see cref="TransformationError"/> (AC-COF-002.3, AC-COF-005.4).
/// </summary>
public abstract class TransformerBase<TSource, TDest> : ITransformer<TSource, TDest>
{
    private readonly IMappingConfigurationRepository _mappingRepository;
    private readonly ITransformationRuleEvaluator _ruleEvaluator;

    protected TransformerBase(
        IMappingConfigurationRepository mappingRepository,
        ITransformationRuleEvaluator ruleEvaluator)
    {
        _mappingRepository = mappingRepository;
        _ruleEvaluator = ruleEvaluator;
    }

    public abstract SystemName SourceSystem { get; }

    public abstract SystemName DestinationSystem { get; }

    /// <summary>Destination fields that must be produced; absence yields a transformation error.</summary>
    protected virtual ISet<string> RequiredDestinationFields { get; } = new HashSet<string>();

    /// <summary>Flattens the typed source payload into a field-name → value dictionary.</summary>
    protected abstract IReadOnlyDictionary<string, object?> ExtractFields(TSource source);

    /// <summary>Builds the typed destination payload from the mapped field dictionary.</summary>
    protected abstract TDest BuildDestination(IReadOnlyDictionary<string, object?> mappedFields, TSource source);

    public async Task<TransformResult<TDest>> TransformAsync(TSource source, string interfaceName, CancellationToken cancellationToken = default)
    {
        // Field rules for the tenant's (source, destination) pair + flow; empty → per-field defaults.
        var mappings = await _mappingRepository.GetActiveForFlowAsync(SourceSystem, DestinationSystem, interfaceName, cancellationToken);
        var sourceFields = ExtractFields(source);
        var mapped = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<TransformationError>();

        foreach (var mapping in mappings)
        {
            if (!sourceFields.TryGetValue(mapping.SourceField, out var sourceValue))
            {
                // Missing source field: error only if the destination is required.
                if (RequiredDestinationFields.Contains(mapping.DestinationField))
                {
                    errors.Add(new TransformationError(mapping.SourceField,
                        $"Required source field '{mapping.SourceField}' is missing."));
                }

                continue;
            }

            mapped[mapping.DestinationField] = _ruleEvaluator.Evaluate(mapping.TransformationRule, sourceValue, sourceFields);
        }

        // Required destination fields that no active mapping produced.
        foreach (var required in RequiredDestinationFields)
        {
            if (!mapped.ContainsKey(required))
            {
                errors.Add(new TransformationError(required,
                    $"Required destination field '{required}' has no active mapping."));
            }
        }

        return errors.Count > 0
            ? TransformResult<TDest>.Fail(errors)
            : TransformResult<TDest>.Ok(BuildDestination(mapped, source));
    }
}
