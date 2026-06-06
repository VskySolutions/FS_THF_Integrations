using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntegrationHub.Infrastructure.Persistence.Configurations;

internal sealed class MappingConfigurationConfiguration : IEntityTypeConfiguration<MappingConfiguration>
{
    public void Configure(EntityTypeBuilder<MappingConfiguration> builder)
    {
        builder.ToTable("MappingConfigurations");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.InterfaceName)
            .HasMaxLength(200);

        builder.Property(m => m.SourceSystem)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(m => m.TargetSystem)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(m => m.SourceField)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(m => m.DestinationField)
            .IsRequired()
            .HasMaxLength(200);

        // Rule expression; bounded but generous.
        builder.Property(m => m.TransformationRule)
            .HasMaxLength(2000);

        // Optional auxiliary metadata; nvarchar(max).
        builder.Property(m => m.MappingJson);

        builder.Property(m => m.IsActive)
            .IsRequired();

        builder.Property(m => m.Version)
            .IsRequired();

        builder.Property(m => m.CreatedAtUtc)
            .IsRequired();

        // Transformer reads active rules for a (source, destination) system pair.
        builder.HasIndex(m => new { m.SourceSystem, m.TargetSystem, m.IsActive });
        builder.HasIndex(m => new { m.InterfaceName, m.IsActive });
    }
}
