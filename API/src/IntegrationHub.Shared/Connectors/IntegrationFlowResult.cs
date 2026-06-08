namespace IntegrationHub.Shared.Connectors;

/// <summary>
/// Outcome of an <c>IIntegrationService.ExecuteAsync</c> run. On failure it records the
/// step at which the flow halted so the job tracking layer can surface it (AC-COF-004.2).
/// </summary>
public sealed class IntegrationFlowResult
{
    public bool Success { get; init; }

    /// <summary>The step that failed (e.g. "Fetch", "Validate", "Transform", "Push"); null on success.</summary>
    public string? FailedStep { get; init; }

    public string? ErrorMessage { get; init; }

    public bool IsRetriable { get; init; }

    public static IntegrationFlowResult Ok() => new() { Success = true };

    public static IntegrationFlowResult Fail(string failedStep, string errorMessage, bool isRetriable)
        => new() { Success = false, FailedStep = failedStep, ErrorMessage = errorMessage, IsRetriable = isRetriable };
}
