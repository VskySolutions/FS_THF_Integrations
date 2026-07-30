using EmsPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmsPortal.Infrastructure.Persistence.Configurations;

// EF Core configurations for the REMS request root and its file links (WO-110). Table names keep the
// REMS casing. Every table is tenant-owned (TenantId FK → Tenant, Restrict) and soft-deletable;
// non-deleted unique indexes are filtered on [Deleted] = 0.

internal sealed class RemsConfiguration : IEntityTypeConfiguration<REMS>
{
    public void Configure(EntityTypeBuilder<REMS> builder)
    {
        builder.ToTable("REMS");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.REMSNumber).IsRequired().HasMaxLength(64);
        builder.Property(r => r.Title).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Description).HasMaxLength(500);
        builder.Property(r => r.Type).IsRequired().HasMaxLength(64);
        builder.Property(r => r.Priority).IsRequired().HasMaxLength(64);
        builder.Property(r => r.Status).IsRequired().HasMaxLength(64);
        builder.Property(r => r.RequestedClientName).IsRequired().HasMaxLength(200);
        builder.Property(r => r.CustomerEmail).HasMaxLength(256);
        builder.Property(r => r.CustomerMobileNumber).HasMaxLength(32);

        builder.HasOne<Tenant>().WithMany().HasForeignKey(r => r.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => r.TenantId);

        builder.HasOne<User>().WithMany().HasForeignKey(r => r.AdminAssignedToId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.CSEId).OnDelete(DeleteBehavior.Restrict);

        // One active request number per tenant.
        builder.HasIndex(r => new { r.TenantId, r.REMSNumber }).IsUnique().HasFilter("[Deleted] = 0");

        // Admin work pool and partner (creator) views.
        builder.HasIndex(r => new { r.TenantId, r.Status, r.AdminAssignedToId, r.CreatedOnUtc });
        builder.HasIndex(r => new { r.TenantId, r.CreatedById, r.Status });
    }
}

internal sealed class RemsFilesConfiguration : IEntityTypeConfiguration<REMSFiles>
{
    public void Configure(EntityTypeBuilder<REMSFiles> builder)
    {
        builder.ToTable("REMSFiles");
        builder.HasKey(f => f.Id);

        builder.HasOne<Tenant>().WithMany().HasForeignKey(f => f.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(f => f.TenantId);

        builder.HasOne(f => f.Rems).WithMany(r => r.Files).HasForeignKey(f => f.REMSId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(f => f.Media).WithMany().HasForeignKey(f => f.MediaId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(f => new { f.TenantId, f.REMSId, f.MediaId }).IsUnique().HasFilter("[Deleted] = 0");
    }
}
