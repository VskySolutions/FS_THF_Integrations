namespace IntegrationHub.Domain.Entities;

/// <summary>
/// A platform user account. Credentials are PBKDF2-hashed; <see cref="TokenVersion"/>
/// is incremented on password change, deactivation, email change, and logout to
/// invalidate outstanding JWTs (Admin User &amp; Role Management).
/// </summary>
public class User
{
    public Guid Id { get; set; }

    /// <summary>Unique login email.</summary>
    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Base64 PBKDF2 hash of the password.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Base64 per-user salt.</summary>
    public string Salt { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>Forces a password change before non-auth API access (new accounts).</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>Session-invalidation counter embedded in issued JWTs.</summary>
    public int TokenVersion { get; set; }

    public DateTime CreatedDate { get; set; }

    /// <summary>The user's tenant/role assignments.</summary>
    public ICollection<UserTenantRole> TenantRoles { get; set; } = new List<UserTenantRole>();
}
