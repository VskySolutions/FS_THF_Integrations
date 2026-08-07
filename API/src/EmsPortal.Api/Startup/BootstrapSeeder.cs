using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Application.Abstractions.Security;
using EmsPortal.Application.Email;
using EmsPortal.Application.OptionSets;
using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;
using EmsPortal.Shared.Security;

namespace EmsPortal.Api.Startup;

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
        var persons = services.GetRequiredService<IPersonRepository>();

        // System RBAC roles are seeded/refreshed on every startup (independent of users).
        await SeedSystemRolesAsync(roles, unitOfWork, cancellationToken);

        // Platform-default email templates are inserted when missing (never overwriting Super Admin edits).
        await SeedEmailTemplatesAsync(
            services.GetRequiredService<IEmailTemplateRepository>(), unitOfWork, cancellationToken);

        // Platform-standard option lists (e.g. Payment Terms) are inserted when missing.
        await SeedOptionSetsAsync(
            services.GetRequiredService<IOptionSetRepository>(), unitOfWork, cancellationToken);

        // If any user already exists, the platform is initialized.
        if (await users.EmailExistsAsync(GetValue(configuration, "Email", "admin@integrationhub.local"), cancellationToken)
            || (await users.ListAsync(null, null, null, null, null, null, null, null, 1, 1, cancellationToken)).Total > 0)
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

        // Seeded just above; required for the bootstrap admin's assignment (RoleId is non-nullable).
        var superAdminRole = await roles.GetByNameAsync(Roles.SuperAdmin, cancellationToken)
            ?? throw new InvalidOperationException("The SuperAdmin system role was not seeded.");

        var password = GetValue(configuration, "Password", "ChangeMe123!");
        var (hash, salt) = hasher.Hash(password);
        var email = GetValue(configuration, "Email", "admin@integrationhub.local");
        var adminId = Guid.NewGuid();

        // The bootstrap admin's personal profile lives on a Person master record (WO-61).
        var person = new Person
        {
            Id = Guid.NewGuid(),
            PersonCode = "PER-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            UserId = adminId,
            FirstName = "Bootstrap",
            LastName = "Super Admin",
            DisplayName = "Bootstrap Super Admin",
            PrimaryEmail = email,
            IsActive = true,
        };
        await persons.AddAsync(person, cancellationToken);

        var admin = new User
        {
            Id = adminId,
            Email = email,
            DisplayName = "Bootstrap Super Admin",
            PersonId = person.Id,
            PasswordHash = hash,
            Salt = salt,
            IsActive = true,
            MustChangePassword = false,
            TokenVersion = 1,
            CreatedDate = DateTime.UtcNow,
            TenantRoles =
            {
                new UserTenantRole { Id = Guid.NewGuid(), TenantId = tenant.Id, Role = UserRole.SuperAdmin, RoleId = superAdminRole.Id },
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
            (Roles.TenantAdmin, "Manage a tenant's users and configuration.", Permissions.ForTenantAdmin()),
            // REMS operational roles (WO-122). Assigned per (user, tenant), stackable with other roles.
            (Roles.Partner, "REMS Partner: create and manage their own requests.", Permissions.ForPartner()),
            (Roles.Admin, "REMS Admin: full request lifecycle, pool, forms, engagements, approvals routing and email log, plus deciding approval tasks assigned to them (as CSE or commission recipient).", Permissions.ForAdmin()),
            (Roles.Approver, "REMS Approver: act on approval tasks assigned to them (record-scoped).", Permissions.ForApprover()),
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

    /// <summary>
    /// Seeds the platform-wide default email templates (one per <see cref="EmailTemplateKey"/>). Idempotent
    /// and non-destructive: a default is inserted only when absent, so Super Admin edits to the global
    /// templates survive restarts.
    /// </summary>
    private static async Task SeedEmailTemplatesAsync(IEmailTemplateRepository templates, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        var added = false;
        foreach (var def in DefaultEmailTemplates.All)
        {
            if (await templates.GetAsync(null, def.Key, cancellationToken) is not null)
            {
                continue;
            }

            await templates.AddAsync(new EmailTemplate
            {
                Id = Guid.NewGuid(),
                TenantId = null,
                TemplateKey = def.Key,
                Subject = def.Subject,
                Body = def.Body,
            }, cancellationToken);
            added = true;
        }

        if (added)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Seeds the platform-standard option lists (TenantId = null, IsSystem = true). Idempotent and
    /// non-destructive: a standard list is inserted only when its key is absent, so tenant additions
    /// and any later edits to the standard items survive restarts.
    /// </summary>
    private static async Task SeedOptionSetsAsync(IOptionSetRepository sets, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        var added = false;
        foreach (var def in DefaultOptionSets.All)
        {
            if (await sets.KeyExistsAsync(null, def.EntityType, def.Key, excludeId: null, cancellationToken))
            {
                continue;
            }

            var set = new OptionSet
            {
                Id = Guid.NewGuid(),
                TenantId = null,
                EntityType = def.EntityType,
                Key = def.Key,
                Name = def.Name,
                ItemSortMode = def.ItemSortMode,
                IsSystem = true,
                IsActive = true,
                Items = def.Items.Select(i => new OptionSetItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = null,
                    Value = i.Value,
                    Label = i.Label,
                    SortOrder = i.SortOrder,
                    IsActive = true,
                    MetadataJson = i.MetadataJson,
                }).ToList(),
            };

            await sets.AddSetAsync(set, cancellationToken);
            added = true;
        }

        if (added)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private static string GetValue(IConfiguration configuration, string key, string fallback)
    {
        var value = configuration[$"Bootstrap:{key}"];
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
