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
        // The partner's message is client-facing now and holds pasted correspondence, so it is uncapped.
        // The old nvarchar(500) measured the markup rather than the words in it, which a forwarded email
        // exceeded almost immediately.
        builder.Property(r => r.Description).HasColumnType("nvarchar(max)");
        builder.Property(r => r.Type).IsRequired().HasMaxLength(64);
        builder.Property(r => r.Status).IsRequired().HasMaxLength(64);
        builder.Property(r => r.RequestedClientName).IsRequired().HasMaxLength(200);
        // Room for a written-out suffix ("Junior") as well as the abbreviations offered, and no more:
        // this is a name particle, not a second name field.
        builder.Property(r => r.ClientNameSuffix).HasMaxLength(16);
        builder.Property(r => r.CustomerEmail).HasMaxLength(256);
        builder.Property(r => r.CustomerMobileNumber).HasMaxLength(32);

        builder.HasOne<Tenant>().WithMany().HasForeignKey(r => r.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => r.TenantId);

        builder.HasOne<User>().WithMany().HasForeignKey(r => r.AdminAssignedToId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.CSEId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.OnBehalfOfUserId).OnDelete(DeleteBehavior.Restrict);

        // "Everything raised for me", including what a delegate prepared — the principal's own list.
        builder.HasIndex(r => new { r.TenantId, r.OnBehalfOfUserId, r.Status });

        // The client's Person master record. A real FK, unlike the loose ExistingClientReferenceId beside
        // it: a person may be shared by many requests and may later become a User, so the link has to hold.
        // Restrict, like every other FK here — purging a REMS request leaves the person standing, and a
        // person cannot be purged out from under the requests that name them as the client.
        builder.HasOne(r => r.ClientPerson).WithMany().HasForeignKey(r => r.ClientPersonId).OnDelete(DeleteBehavior.Restrict);

        // "Every request for this client" — the read behind a client's history, and behind converting one
        // into a user.
        builder.HasIndex(r => new { r.TenantId, r.ClientPersonId });

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

internal sealed class RemsAdditionalEntityConfiguration : IEntityTypeConfiguration<REMSAdditionalEntity>
{
    public void Configure(EntityTypeBuilder<REMSAdditionalEntity> builder)
    {
        builder.ToTable("REMSAdditionalEntity");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.SourceKey).IsRequired().HasMaxLength(64);
        builder.Property(a => a.FullName).IsRequired().HasMaxLength(200);
        builder.Property(a => a.EmailAddress).HasMaxLength(256);
        builder.Property(a => a.PhoneNumber).HasMaxLength(32);

        builder.HasOne<Tenant>().WithMany().HasForeignKey(a => a.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => a.TenantId);

        builder.HasOne(a => a.Rems).WithMany(r => r.AdditionalEntities).HasForeignKey(a => a.REMSId).OnDelete(DeleteBehavior.Restrict);

        // CreatedREMSId is deliberately NOT a foreign key. The follow-up request stands on its own — it is
        // not a child of the request that revealed it — and modelling it as a relationship would invite
        // exactly the parent/child reads that decision rules out.

        builder.HasIndex(a => new { a.TenantId, a.REMSId, a.SourceKey }).IsUnique().HasFilter("[Deleted] = 0");

        // "Which of this client's other businesses still need an EMS" — the read behind the list flag.
        builder.HasIndex(a => new { a.TenantId, a.REMSId, a.CreatedREMSId });
    }
}

internal sealed class RemsDelegationConfiguration : IEntityTypeConfiguration<REMSDelegation>
{
    public void Configure(EntityTypeBuilder<REMSDelegation> builder)
    {
        builder.ToTable("REMSDelegation", t => t.HasCheckConstraint(
            "CK_REMSDelegation_Dates",
            "[StartsOn] IS NULL OR [EndsOn] IS NULL OR [EndsOn] >= [StartsOn]"));
        builder.HasKey(d => d.Id);

        builder.HasOne<Tenant>().WithMany().HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(d => d.TenantId);

        builder.HasOne(d => d.Principal).WithMany().HasForeignKey(d => d.PrincipalUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Delegate).WithMany().HasForeignKey(d => d.DelegateUserId).OnDelete(DeleteBehavior.Restrict);

        // One live grant per pair. Re-granting with different rights edits the row rather than stacking a
        // second one, so there is never a question of which of two grants applies.
        builder.HasIndex(d => new { d.TenantId, d.PrincipalUserId, d.DelegateUserId })
            .IsUnique()
            .HasFilter("[Deleted] = 0");

        // "Who can I act for?" — the read behind the acting-as picker.
        builder.HasIndex(d => new { d.TenantId, d.DelegateUserId });
    }
}

internal sealed class RemsSendBackConfiguration : IEntityTypeConfiguration<REMSSendBack>
{
    public void Configure(EntityTypeBuilder<REMSSendBack> builder)
    {
        builder.ToTable("REMSSendBack");
        builder.HasKey(s => s.Id);

        // Matches the request's own description: an admin explaining what is wrong with the setup should
        // not be rationed, and a truncated reason is not actionable.
        builder.Property(s => s.Reason).IsRequired().HasColumnType("nvarchar(max)");

        builder.HasOne<Tenant>().WithMany().HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => s.TenantId);

        builder.HasOne(s => s.Rems).WithMany(r => r.SendBacks).HasForeignKey(s => s.REMSId).OnDelete(DeleteBehavior.Restrict);

        // At most one unresolved return per request: the loop is sequential, so a second open return would
        // mean the request was in two places at once.
        builder.HasIndex(s => new { s.TenantId, s.REMSId })
            .IsUnique()
            .HasFilter("[Deleted] = 0 AND [ResolvedOnUtc] IS NULL");

        // The history read, newest last.
        builder.HasIndex(s => new { s.TenantId, s.REMSId, s.CreatedOnUtc });
    }
}
