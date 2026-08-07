using EmsPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmsPortal.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for a user's department placement within a tenant. The two filtered unique
/// indexes are the invariants: one department per user, and one head per department.
/// </summary>
internal sealed class UserDepartmentConfiguration : IEntityTypeConfiguration<UserDepartment>
{
    public void Configure(EntityTypeBuilder<UserDepartment> builder)
    {
        builder.ToTable("UserDepartments");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Department).IsRequired().HasMaxLength(64);

        builder.HasOne<Tenant>().WithMany().HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);

        // One active department per user per tenant.
        builder.HasIndex(d => new { d.TenantId, d.UserId }).IsUnique().HasFilter("[Deleted] = 0");

        // At most one head per (tenant, department). This is the database-level guarantee behind the
        // head handover — the controller demotes the incumbent in a separate SaveChanges (inside one
        // transaction) so the two rows never both claim the department.
        builder.HasIndex(d => new { d.TenantId, d.Department })
            .IsUnique()
            .HasFilter("[Deleted] = 0 AND [IsHead] = 1")
            .HasDatabaseName("IX_UserDepartments_TenantId_Department_Head");
    }
}
