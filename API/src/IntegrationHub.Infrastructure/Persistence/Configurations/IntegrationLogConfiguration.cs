using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntegrationHub.Infrastructure.Persistence.Configurations;

internal sealed class IntegrationLogConfiguration : IEntityTypeConfiguration<IntegrationLog>
{
    public void Configure(EntityTypeBuilder<IntegrationLog> builder)
    {
        builder.ToTable("IntegrationLogs");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .ValueGeneratedOnAdd();

        builder.Property(l => l.Level)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(l => l.Message)
            .HasMaxLength(4000);

        // Payloads can be large; map to nvarchar(max).
        builder.Property(l => l.RequestPayload);
        builder.Property(l => l.ResponsePayload);

        builder.HasIndex(l => l.JobId);
        builder.HasIndex(l => l.TenantId);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(l => l.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
