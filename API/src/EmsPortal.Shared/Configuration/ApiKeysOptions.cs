namespace EmsPortal.Shared.Configuration;

/// <summary>
/// Registered machine-to-machine API keys. Only PBKDF2 hashes are stored — plaintext
/// keys are never persisted. Later work orders move this store into the database;
/// configuration binding keeps it functional in the meantime.
/// </summary>
public sealed class ApiKeysOptions
{
    public List<ApiKeyEntry> Keys { get; set; } = new();
}

/// <summary>A single registered API key principal and its PBKDF2 hash material.</summary>
public sealed class ApiKeyEntry
{
    /// <summary>Display name of the machine client.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Role granted to requests authenticated with this key.</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Active tenant id assigned to this key, if tenant-scoped.</summary>
    public string? TenantId { get; set; }

    /// <summary>Base64-encoded per-key salt.</summary>
    public string Salt { get; set; } = string.Empty;

    /// <summary>Base64-encoded PBKDF2 hash of the plaintext key.</summary>
    public string Hash { get; set; } = string.Empty;
}
