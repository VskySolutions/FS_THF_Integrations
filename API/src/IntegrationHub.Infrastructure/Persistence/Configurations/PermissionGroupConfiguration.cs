using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntegrationHub.Infrastructure.Persistence.Configurations;

internal sealed class PermissionGroupConfiguration : IEntityTypeConfiguration<PermissionGroup>
{
    public void Configure(EntityTypeBuilder<PermissionGroup> builder)
    {
        builder.ToTable("PermissionGroups");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name).IsRequired().HasMaxLength(150);
        builder.Property(g => g.Description).HasMaxLength(500);
        builder.Property(g => g.IsActive).IsRequired();

        // Unique per (tenant, name); names may repeat across tenants.
        builder.HasIndex(g => new { g.TenantId, g.Name }).IsUnique().HasFilter("[Deleted] = 0");
        builder.HasIndex(g => g.TenantId);

        builder.HasOne(g => g.Tenant)
            .WithMany()
            .HasForeignKey(g => g.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(g => g.Permissions)
            .WithOne(p => p.PermissionGroup!)
            .HasForeignKey(p => p.PermissionGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(g => g.RoleLinks)
            .WithOne(l => l.PermissionGroup!)
            .HasForeignKey(l => l.PermissionGroupId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PermissionGroupPermissionConfiguration : IEntityTypeConfiguration<PermissionGroupPermission>
{
    public void Configure(EntityTypeBuilder<PermissionGroupPermission> builder)
    {
        builder.ToTable("PermissionGroupPermissions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.PermissionKey).IsRequired().HasMaxLength(100);

        builder.HasIndex(p => p.PermissionGroupId);
        builder.HasIndex(p => new { p.PermissionGroupId, p.PermissionKey }).IsUnique();
    }
}

internal sealed class RolePermissionGroupConfiguration : IEntityTypeConfiguration<RolePermissionGroup>
{
    public void Configure(EntityTypeBuilder<RolePermissionGroup> builder)
    {
        builder.ToTable("RolePermissionGroups");

        builder.HasKey(l => l.Id);

        builder.HasIndex(l => l.RoleId);
        builder.HasIndex(l => l.PermissionGroupId);
        builder.HasIndex(l => new { l.RoleId, l.PermissionGroupId }).IsUnique();

        builder.HasOne(l => l.Role)
            .WithMany(r => r.GroupLinks)
            .HasForeignKey(l => l.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
