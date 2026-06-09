using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Security;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Shared.Security;

namespace IntegrationHub.Api.Startup;

/// <summary>
/// Seeds a bootstrap Super Admin (and a default tenant) on first startup when the platform
/// has no users, so the system is usable out of the box. Credentials come from the
/// <c>Bootstrap</c> configuration section. Idempotent: does nothing once any user exists.
/// </summary>
public static class BootstrapSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var users = services.GetRequiredService<IUserRepository>();
        var tenants = services.GetRequiredService<ITenantRepository>();
        var hasher = services.GetRequiredService<IPasswordHasher>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var roles = services.GetRequiredService<IRoleRepository>();

        // System RBAC roles are seeded/refreshed on every startup (independent of users).
        await SeedSystemRolesAsync(roles, unitOfWork, cancellationToken);

        // If any user already exists, the platform is initialized.
        if (await users.EmailExistsAsync(GetValue(configuration, "Email", "admin@integrationhub.local"), cancellationToken)
            || (await users.ListAsync(null, 1, 1, cancellationToken)).Total > 0)
        {
            return;
        }

        var tenantIdentifier = GetValue(configuration, "TenantIdentifier", "system");
        var tenant = await tenants.GetByIdentifierAsync(tenantIdentifier, cancellationToken);
        if (tenant is null)
        {
            tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = GetValue(configuration, "TenantName", "System"),
                Identifier = tenantIdentifier,
                Status = TenantStatus.Active,
                CreatedDate = DateTime.UtcNow,
            };
            await tenants.AddAsync(tenant, cancellationToken);
        }

        var password = GetValue(configuration, "Password", "ChangeMe123!");
        var (hash, salt) = hasher.Hash(password);
        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = GetValue(configuration, "Email", "admin@integrationhub.local"),
            DisplayName = "Bootstrap Super Admin",
            PasswordHash = hash,
            Salt = salt,
            IsActive = true,
            MustChangePassword = false,
            TokenVersion = 1,
            CreatedDate = DateTime.UtcNow,
            TenantRoles =
            {
                new UserTenantRole { Id = Guid.NewGuid(), TenantId = tenant.Id, Role = UserRole.SuperAdmin },
            },
        };
        await users.AddAsync(admin, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedSystemRolesAsync(IRoleRepository roles, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        var definitions = new (string Name, string Description, IReadOnlyList<string> Permissions)[]
        {
            (Roles.SuperAdmin, "Full platform access.", Permissions.ForSuperAdmin()),
            (Roles.TenantAdmin, "Manage a tenant's users, mappings and integrations.", Permissions.ForTenantAdmin()),
            (Roles.Operator, "Trigger and monitor integrations.", Permissions.ForOperator()),
        };

        foreach (var (name, description, permissions) in definitions)
        {
            var existing = await roles.GetByNameAsync(name, cancellationToken);
            if (existing is null)
            {
                await roles.AddAsync(new Role
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Description = description,
                    IsSystem = true,
                    Permissions = permissions.ToList(),
                }, cancellationToken);
            }
            else
            {
                // Keep the seeded permission set authoritative for system roles.
                existing.IsSystem = true;
                existing.Permissions = permissions.ToList();
                roles.Update(existing);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string GetValue(IConfiguration configuration, string key, string fallback)
    {
        var value = configuration[$"Bootstrap:{key}"];
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
