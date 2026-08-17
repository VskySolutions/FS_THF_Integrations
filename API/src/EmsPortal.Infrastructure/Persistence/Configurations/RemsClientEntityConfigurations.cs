using EmsPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmsPortal.Infrastructure.Persistence.Configurations;

// EF Core configurations for the REMS client materialised from a submission, its entities, and each
// entity's addresses and contacts (WO-110).

internal sealed class RemsClientConfiguration : IEntityTypeConfiguration<REMSClient>
{
    public void Configure(EntityTypeBuilder<REMSClient> builder)
    {
        builder.ToTable("REMSClient");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Email).IsRequired().HasMaxLength(256);
        builder.Property(c => c.MobileNumber).HasMaxLength(32);
        builder.Property(c => c.ReferralSource).HasMaxLength(64);
        builder.Property(c => c.ReferralSourceDetail).HasMaxLength(256);
        builder.Property(c => c.BillingContactName).HasMaxLength(200);
        builder.Property(c => c.BillingEmail).HasMaxLength(256);

        builder.HasOne<Tenant>().WithMany().HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => c.TenantId);

        builder.HasOne(c => c.Rems).WithMany(r => r.Clients).HasForeignKey(c => c.REMSId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.SourceFormSubmission).WithMany().HasForeignKey(c => c.SourceFormSubmissionId).OnDelete(DeleteBehavior.Restrict);

        // One active client per request.
        builder.HasIndex(c => new { c.TenantId, c.REMSId }).IsUnique().HasFilter("[Deleted] = 0");
    }
}

internal sealed class RemsEntityConfiguration : IEntityTypeConfiguration<REMSEntity>
{
    public void Configure(EntityTypeBuilder<REMSEntity> builder)
    {
        builder.ToTable("REMSEntity");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.SourceEntityKey).IsRequired().HasMaxLength(64);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.EIN).HasMaxLength(32);

        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.TenantId);

        builder.HasOne(e => e.Client).WithMany(c => c.Entities).HasForeignKey(e => e.REMSClientId).OnDelete(DeleteBehavior.Restrict);

        // Unique source key per client, and exactly one active main entity per client. The main-entity
        // index is given an explicit name so it stays distinct from the plain nav index on the same
        // columns below (an unnamed HasIndex on the same property set would merge into one index).
        builder.HasIndex(e => new { e.TenantId, e.REMSClientId, e.SourceEntityKey }).IsUnique().HasFilter("[Deleted] = 0");
        builder.HasIndex(e => new { e.TenantId, e.REMSClientId }, "IX_REMSEntity_TenantId_REMSClientId_Main")
            .IsUnique()
            .HasFilter("[IsMainEntity] = 1 AND [Deleted] = 0");

        // Navigation lookup by client.
        builder.HasIndex(e => new { e.TenantId, e.REMSClientId });
    }
}

internal sealed class RemsEntityAddressConfiguration : IEntityTypeConfiguration<REMSEntityAddress>
{
    public void Configure(EntityTypeBuilder<REMSEntityAddress> builder)
    {
        builder.ToTable("REMSEntityAddress");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.AddressType).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasOne<Tenant>().WithMany().HasForeignKey(a => a.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => a.TenantId);

        builder.HasOne(a => a.Entity).WithMany(e => e.Addresses).HasForeignKey(a => a.REMSEntityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Address).WithMany().HasForeignKey(a => a.AddressId).OnDelete(DeleteBehavior.Restrict);

        // One address per (entity, type) — now Physical, Mailing AND Billing. The billing address used to
        // sit on REMSClient with its own FK; it is an entity address like the other two, so the same rule
        // covers it.
        builder.HasIndex(a => new { a.TenantId, a.REMSEntityId, a.AddressType }).IsUnique().HasFilter("[Deleted] = 0");
    }
}

internal sealed class RemsEntityContactConfiguration : IEntityTypeConfiguration<REMSEntityContact>
{
    public void Configure(EntityTypeBuilder<REMSEntityContact> builder)
    {
        builder.ToTable("REMSEntityContact");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.ContactRole).IsRequired().HasMaxLength(64);

        builder.HasOne<Tenant>().WithMany().HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => c.TenantId);

        builder.HasOne(c => c.Entity).WithMany(e => e.Contacts).HasForeignKey(c => c.REMSEntityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.Person).WithMany().HasForeignKey(c => c.PersonId).OnDelete(DeleteBehavior.Restrict);

        // One contact per (entity, role).
        builder.HasIndex(c => new { c.TenantId, c.REMSEntityId, c.ContactRole }).IsUnique().HasFilter("[Deleted] = 0");
    }
}
