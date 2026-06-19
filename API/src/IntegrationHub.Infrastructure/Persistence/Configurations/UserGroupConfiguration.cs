using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntegrationHub.Infrastructure.Persistence.Configurations;

internal sealed class UserGroupConfiguration : IEntityTypeConfiguration<UserGroup>
{
    public void Configure(EntityTypeBuilder<UserGroup> builder)
    {
        builder.ToTable("UserGroups");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name).IsRequired().HasMaxLength(150);
        builder.Property(g => g.Description).HasMaxLength(500);

        // Unique per (tenant, name); names may repeat across tenants.
        builder.HasIndex(g => new { g.TenantId, g.Name }).IsUnique().HasFilter("[Deleted] = 0");
        builder.HasIndex(g => g.TenantId);

        builder.HasOne(g => g.Tenant)
            .WithMany()
            .HasForeignKey(g => g.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(g => g.Members)
            .WithOne(m => m.UserGroup!)
            .HasForeignKey(m => m.UserGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UserGroupMemberConfiguration : IEntityTypeConfiguration<UserGroupMember>
{
    public void Configure(EntityTypeBuilder<UserGroupMember> builder)
    {
        builder.ToTable("UserGroupMembers");

        builder.HasKey(m => m.Id);

        builder.HasIndex(m => m.UserGroupId);
        builder.HasIndex(m => m.UserId);
        builder.HasIndex(m => m.TenantId);
        builder.HasIndex(m => new { m.UserGroupId, m.UserId }).IsUnique().HasFilter("[Deleted] = 0");

        builder.HasOne(m => m.User)
            .WithMany(u => u.GroupMemberships)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
