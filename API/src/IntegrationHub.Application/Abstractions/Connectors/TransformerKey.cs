using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Application.Abstractions.Connectors;

/// <summary>
/// DI key identifying a transformer by its (source, destination) system pair. Used for
/// keyed registration and resolution so different flow permutations
/// (e.g. Concur→Maconomy) resolve the correct <c>ITransformer</c> without concrete-type
/// coupling (Connector Framework ADR-001).
/// </summary>
public readonly record struct TransformerKey(SystemName Source, SystemName Destination);
