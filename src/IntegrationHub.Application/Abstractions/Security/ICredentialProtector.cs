namespace IntegrationHub.Application.Abstractions.Security;

/// <summary>
/// Protects and unprotects credential blobs at rest. The production implementation is
/// backed by the .NET Data Protection API (Multi-Tenancy ADR-002, delivered in a separate
/// Phase 2 work order); a passthrough implementation is used until then.
/// </summary>
public interface ICredentialProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedValue);
}
