using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntegrationHub.Infrastructure.Persistence.Configurations;

internal sealed class CustomerAuditEntryConfiguration : IEntityTypeConfiguration<CustomerAuditEntry>
{
    public void Configure(EntityTypeBuilder<CustomerAuditEntry> builder)
    {
        builder.ToTable("CustomerAuditEntries");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.ActionType).HasConversion<int>().IsRequired();
        builder.Property(a => a.PerformedBy).HasMaxLength(200);
        builder.Property(a => a.Notes).HasMaxLength(4000);
        builder.Property(a => a.FieldsAffected).HasMaxLength(4000);
        builder.Property(a => a.PerformedOnUtc).IsRequired();

        builder.HasIndex(a => a.CustomerRequestId);
        builder.HasIndex(a => a.TenantId);
    }
}
