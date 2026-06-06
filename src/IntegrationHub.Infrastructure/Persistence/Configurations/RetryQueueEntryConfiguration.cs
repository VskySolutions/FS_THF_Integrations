using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntegrationHub.Infrastructure.Persistence.Configurations;

internal sealed class RetryQueueEntryConfiguration : IEntityTypeConfiguration<RetryQueueEntry>
{
    public void Configure(EntityTypeBuilder<RetryQueueEntry> builder)
    {
        // Blueprint table name is "RetryQueue".
        builder.ToTable("RetryQueue");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.RetryCount)
            .IsRequired();

        builder.Property(r => r.NextRetryDate)
            .IsRequired();

        builder.Property(r => r.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(r => r.LastError)
            .HasMaxLength(2000);

        builder.Property(r => r.CreatedAtUtc)
            .IsRequired();

        // The RetryJobScheduler polls for due, pending entries.
        builder.HasIndex(r => new { r.Status, r.NextRetryDate });
        builder.HasIndex(r => r.JobId);
        builder.HasIndex(r => r.TenantId);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(r => r.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Job)
            .WithMany()
            .HasForeignKey(r => r.JobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
