namespace IntegrationHub.Application.Abstractions.Security;

/// <summary>
/// Encrypts and decrypts tenant API credential blobs, hiding the .NET Data Protection
/// internals from callers (Multi-Tenancy ADR-002). Stored values are always ciphertext;
/// plaintext secrets are never persisted, logged, or returned in API responses.
/// </summary>
public interface ICredentialEncryptionService
{
    string Encrypt(string plaintext);

    string Decrypt(string ciphertext);
}
