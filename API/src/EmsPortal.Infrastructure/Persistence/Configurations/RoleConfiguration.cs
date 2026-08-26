using EmsPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmsPortal.Infrastructure.Persistence.Configurations;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.Description)
            .HasMaxLength(500);

        // Permission keys stored as a JSON array column (EF Core primitive collection).
        builder.Property(r => r.Permissions)
            .HasMaxLength(4000);

        // Denormalised cache of group-derived permission keys (Permission Groups, ADR-002).
        builder.Property(r => r.EffectivePermissionsJson);

        // A name is unique within its scope, not across the platform: among the platform roles
        // (TenantId null — SQL Server treats those NULLs as equal, which is exactly the rule wanted here)
        // and within each owning tenant. Two firms may both want a "Reviewer"; neither may take a second
        // one. A tenant name that would shadow a platform role is refused by RolesController — the index
        // cannot see that clash because the TenantId differs.
        builder.HasIndex(r => new { r.TenantId, r.Name }).IsUnique().HasFilter("[Deleted] = 0");
    }
}
