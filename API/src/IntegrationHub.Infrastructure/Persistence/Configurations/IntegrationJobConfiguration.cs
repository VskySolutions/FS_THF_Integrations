using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntegrationHub.Infrastructure.Persistence.Configurations;

internal sealed class IntegrationJobConfiguration : IEntityTypeConfiguration<IntegrationJob>
{
    public void Configure(EntityTypeBuilder<IntegrationJob> builder)
    {
        builder.ToTable("IntegrationJobs");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.InterfaceName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(j => j.Direction)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(j => j.SourceSystem)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(j => j.TargetSystem)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(j => j.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(j => j.CorrelationId)
            .HasMaxLength(100);

        builder.Property(j => j.ErrorMessage)
            .HasMaxLength(2000);

        builder.HasIndex(j => j.Status);
        builder.HasIndex(j => j.CorrelationId);
        builder.HasIndex(j => j.CreatedOnUtc);
        builder.HasIndex(j => j.TenantId);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(j => j.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(j => j.Logs)
            .WithOne(l => l.Job!)
            .HasForeignKey(l => l.JobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
