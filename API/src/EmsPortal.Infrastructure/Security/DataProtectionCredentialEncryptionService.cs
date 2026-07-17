using EmsPortal.Application.Abstractions.Security;
using Microsoft.AspNetCore.DataProtection;

namespace EmsPortal.Infrastructure.Security;

/// <summary>
/// <see cref="ICredentialEncryptionService"/> backed by a purpose-scoped Data Protection
/// protector. The <c>"TenantCredentials"</c> purpose isolates these keys from any other
/// protected data, and the key ring is persisted to SQL Server so all instances share it.
/// </summary>
internal sealed class DataProtectionCredentialEncryptionService : ICredentialEncryptionService
{
    public const string ProtectorPurpose = "TenantCredentials";

    private readonly IDataProtector _protector;

    public DataProtectionCredentialEncryptionService(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
    }

    public string Encrypt(string plaintext) => _protector.Protect(plaintext);

    public string Decrypt(string ciphertext) => _protector.Unprotect(ciphertext);
}
