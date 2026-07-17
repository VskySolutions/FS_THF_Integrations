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

        builder.HasIndex(r => r.Name).IsUnique().HasFilter("[Deleted] = 0");
    }
}
