using IntegrationHub.Application.Abstractions.Security;

namespace IntegrationHub.Infrastructure.Security;

/// <summary>
/// Placeholder credential protector that stores blobs as-is. The Data Protection
/// API-backed implementation is delivered in a separate Phase 2 work order; swapping it
/// in requires no connector or service changes.
/// </summary>
internal sealed class PassthroughCredentialProtector : ICredentialProtector
{
    public string Protect(string plaintext) => plaintext;

    public string Unprotect(string protectedValue) => protectedValue;
}
