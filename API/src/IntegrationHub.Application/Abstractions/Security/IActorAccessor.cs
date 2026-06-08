namespace IntegrationHub.Application.Abstractions.Security;

/// <summary>
/// Resolves the identity of the actor responsible for the current operation. In the
/// API this is the authenticated user principal; in the Background Worker it is the
/// system service identity.
/// </summary>
public interface IActorAccessor
{
    /// <summary>Returns the current actor's identity, falling back to a system identity.</summary>
    string GetCurrentActor();
}
