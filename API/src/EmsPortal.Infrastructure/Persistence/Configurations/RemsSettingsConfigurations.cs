using EmsPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmsPortal.Infrastructure.Persistence.Configurations;

// EF Core configurations for the per-tenant REMS settings (WO-114): one settings row per tenant plus its
// department-to-director mapping child rows. Both are tenant-scoped and soft-deletable.

internal sealed class RemsSettingsConfiguration : IEntityTypeConfiguration<RemsSettings>
{
    public void Configure(EntityTypeBuilder<RemsSettings> builder)
    {
        builder.ToTable("RemsSettings");
        builder.HasKey(s => s.Id);

        builder.HasOne<Tenant>().WithMany().HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);

        // Exactly one active settings row per tenant.
        builder.HasIndex(s => s.TenantId).IsUnique().HasFilter("[Deleted] = 0");
    }
}

internal sealed class RemsDepartmentDirectorConfiguration : IEntityTypeConfiguration<RemsDepartmentDirector>
{
    public void Configure(EntityTypeBuilder<RemsDepartmentDirector> builder)
    {
        builder.ToTable("RemsDepartmentDirector");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Department).IsRequired().HasMaxLength(64);

        builder.HasOne<Tenant>().WithMany().HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(d => d.TenantId);

        builder.HasOne(d => d.Settings).WithMany(s => s.DepartmentDirectors).HasForeignKey(d => d.RemsSettingsId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(d => d.DirectorUserId).OnDelete(DeleteBehavior.Restrict);

        // One director per (tenant, department).
        builder.HasIndex(d => new { d.TenantId, d.Department }).IsUnique().HasFilter("[Deleted] = 0");
    }
}
