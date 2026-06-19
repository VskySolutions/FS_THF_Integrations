using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntegrationHub.Infrastructure.Persistence.Configurations;

internal sealed class CustomerRequestConfiguration : IEntityTypeConfiguration<CustomerRequest>
{
    public void Configure(EntityTypeBuilder<CustomerRequest> builder)
    {
        builder.ToTable("CustomerRequests");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.CustomerRequestNumber).HasMaxLength(32);
        builder.HasIndex(c => c.CustomerRequestNumber).IsUnique().HasFilter("[CustomerRequestNumber] IS NOT NULL AND [Deleted] = 0");

        builder.Property(c => c.Status).HasConversion<int>().IsRequired();

        // Step 1: Basic Information
        builder.Property(c => c.LegalName).IsRequired().HasMaxLength(200);
        builder.Property(c => c.CompanyName).IsRequired().HasMaxLength(200);
        builder.Property(c => c.ContactPerson).HasMaxLength(150);
        builder.Property(c => c.EmailAddress).IsRequired().HasMaxLength(256);
        builder.Property(c => c.PhoneNumber).HasMaxLength(32);
        builder.Property(c => c.Website).HasMaxLength(512);

        // Enrichment: Business Information
        builder.Property(c => c.InternalCustomerCategory).HasMaxLength(128);
        builder.Property(c => c.Territory).HasMaxLength(128);
        builder.Property(c => c.PracticeArea).HasMaxLength(128);
        builder.Property(c => c.SalesRepresentative).HasMaxLength(150);
        builder.Property(c => c.EnrichmentPaymentTerms).HasMaxLength(128);
        builder.Property(c => c.CreditTerms).HasMaxLength(128);
        builder.Property(c => c.CustomerType).HasMaxLength(128);
        builder.Property(c => c.BusinessSegment).HasMaxLength(128);
        builder.Property(c => c.RiskCategory).HasMaxLength(128);

        // Step 2: Maconomy Fields
        builder.Property(c => c.TaxNumber).HasMaxLength(64);
        builder.Property(c => c.RegistrationNumber).HasMaxLength(64);
        builder.Property(c => c.BusinessUnit).HasMaxLength(128);
        builder.Property(c => c.Currency).HasMaxLength(8);
        builder.Property(c => c.CustomerGroup).HasMaxLength(128);
        builder.Property(c => c.PaymentTerms).HasMaxLength(128);
        builder.Property(c => c.CreditLimit).HasColumnType("decimal(18,2)");
        builder.Property(c => c.Industry).HasMaxLength(128);
        builder.Property(c => c.InvoiceLanguage).HasMaxLength(32);
        builder.Property(c => c.BillingEmail).HasMaxLength(256);

        // Workflow metadata
        builder.Property(c => c.MaconomyCustomerNumber).HasMaxLength(64);
        builder.Property(c => c.RejectionReason).HasMaxLength(2000);
        builder.Property(c => c.ReturnNotes).HasMaxLength(2000);
        builder.Property(c => c.UnlockedFields).HasMaxLength(2000);
        builder.Property(c => c.LastSyncError).HasMaxLength(2000);
        builder.Property(c => c.RequiredApprovalStages).HasDefaultValue(1);

        builder.HasIndex(c => c.TenantId);
        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => c.CreatedOnUtc);

        builder.HasOne(c => c.Tenant)
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Shared, reusable address record (the common Address table). No cascade — addresses are shared.
        builder.HasOne(c => c.Address)
            .WithMany()
            .HasForeignKey(c => c.AddressId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.AuditEntries)
            .WithOne(a => a.CustomerRequest!)
            .HasForeignKey(a => a.CustomerRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Documents)
            .WithOne(d => d.CustomerRequest!)
            .HasForeignKey(d => d.CustomerRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
