namespace IntegrationHub.Domain.Enums;

/// <summary>
/// Direction of data flow for an integration job relative to the IntegrationHub platform.
/// </summary>
public enum IntegrationDirection
{
    /// <summary>Data flows from an external system into the platform.</summary>
    Inbound = 0,

    /// <summary>Data flows from the platform out to an external system.</summary>
    Outbound = 1
}
