using IntegrationHub.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationHub.Application.Abstractions.Connectors;

/// <summary>
/// Keyed DI registration helpers for the connector framework. Transformers are registered
/// keyed by their <see cref="TransformerKey"/> so the orchestration layer resolves the
/// right one per (source, destination) pair without referencing concrete classes.
/// </summary>
public static class ConnectorServiceCollectionExtensions
{
    /// <summary>
    /// Registers a transformer implementation keyed by its (source, destination) pair.
    /// Resolve it with <see cref="GetTransformer{TSource,TDest}"/> or
    /// <c>[FromKeyedServices(new TransformerKey(...))]</c>.
    /// </summary>
    public static IServiceCollection AddTransformer<TSource, TDest, TImplementation>(
        this IServiceCollection services,
        SystemName source,
        SystemName destination)
        where TImplementation : class, ITransformer<TSource, TDest>
    {
        services.AddKeyedScoped<ITransformer<TSource, TDest>, TImplementation>(new TransformerKey(source, destination));
        return services;
    }

    /// <summary>Resolves the transformer registered for the given system pair.</summary>
    public static ITransformer<TSource, TDest> GetTransformer<TSource, TDest>(
        this IServiceProvider provider,
        SystemName source,
        SystemName destination)
        => provider.GetRequiredKeyedService<ITransformer<TSource, TDest>>(new TransformerKey(source, destination));
}
