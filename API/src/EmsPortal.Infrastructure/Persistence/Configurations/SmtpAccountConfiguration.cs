using EmsPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmsPortal.Infrastructure.Persistence.Configurations;

internal sealed class SmtpAccountConfiguration : IEntityTypeConfiguration<SmtpAccount>
{
    public void Configure(EntityTypeBuilder<SmtpAccount> builder)
    {
        builder.ToTable("SmtpAccounts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.AccountName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(a => a.Host)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(a => a.Port)
            .IsRequired();

        builder.Property(a => a.EncryptionType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(a => a.AuthType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(a => a.Username)
            .HasMaxLength(255);

        // Encrypted blob; never stored in plaintext, never returned in API responses.
        builder.Property(a => a.EncryptedPassword);

        builder.Property(a => a.FromName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(a => a.FromEmail)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(a => a.IsActive)
            .IsRequired();

        // Account names are unique per tenant (amongst non-deleted rows).
        builder.HasIndex(a => new { a.TenantId, a.AccountName }).IsUnique().HasFilter("[Deleted] = 0");

        // At most one active account per tenant — enforced at the DB level independent of app code
        // (SMTP Email Accounts ADR-003).
        builder.HasIndex(a => a.TenantId)
            .IsUnique()
            .HasFilter("[IsActive] = 1 AND [Deleted] = 0")
            .HasDatabaseName("UX_SmtpAccounts_TenantId_ActiveSingle");

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(a => a.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
