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
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(m => m.SourceSystem)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(m => m.TargetSystem)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // Mapping rule set can be large; map to nvarchar(max).
        builder.Property(m => m.MappingJson)
            .IsRequired();

        builder.Property(m => m.IsActive)
            .IsRequired();

        builder.Property(m => m.Version)
            .IsRequired();

        builder.Property(m => m.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(m => new { m.InterfaceName, m.IsActive });
    }
}
