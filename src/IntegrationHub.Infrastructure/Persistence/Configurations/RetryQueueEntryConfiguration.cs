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

        builder.Property(r => r.AttemptNumber)
            .IsRequired();

        builder.Property(r => r.NextAttemptUtc)
            .IsRequired();

        builder.Property(r => r.LastError)
            .HasMaxLength(2000);

        builder.Property(r => r.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(r => r.NextAttemptUtc);
        builder.HasIndex(r => r.JobId);

        builder.HasOne(r => r.Job)
            .WithMany()
            .HasForeignKey(r => r.JobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
