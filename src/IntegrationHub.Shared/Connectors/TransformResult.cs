namespace IntegrationHub.Shared.Connectors;

/// <summary>Result of an <c>ITransformer</c> invocation: the mapped payload or a list of errors.</summary>
public sealed class TransformResult<T>
{
    public bool Success { get; init; }

    public T? Payload { get; init; }

    public IReadOnlyList<TransformationError> Errors { get; init; } = Array.Empty<TransformationError>();

    public static TransformResult<T> Ok(T payload)
        => new() { Success = true, Payload = payload };

    public static TransformResult<T> Fail(IReadOnlyList<TransformationError> errors)
        => new() { Success = false, Errors = errors };

    public static TransformResult<T> Fail(string field, string message)
        => Fail(new[] { new TransformationError(field, message) });
}

/// <summary>A single field-level transformation failure.</summary>
public sealed record TransformationError(string Field, string Message);
