using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntegrationHub.Infrastructure.Persistence.Configurations;

internal sealed class TenantApiConfigurationConfiguration : IEntityTypeConfiguration<TenantApiConfiguration>
{
    public void Configure(EntityTypeBuilder<TenantApiConfiguration> builder)
    {
        builder.ToTable("TenantApiConfigurations");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.System)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // Encrypted credential blob; never stored in plaintext.
        builder.Property(c => c.EncryptedCredentials)
            .IsRequired();

        builder.Property(c => c.CreatedDate)
            .IsRequired();

        // One configuration per (tenant, system).
        builder.HasIndex(c => new { c.TenantId, c.System }).IsUnique().HasFilter("[Deleted] = 0");
    }
}
