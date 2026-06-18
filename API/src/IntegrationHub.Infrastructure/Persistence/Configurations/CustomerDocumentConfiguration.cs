using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntegrationHub.Infrastructure.Persistence.Configurations;

internal sealed class CustomerDocumentConfiguration : IEntityTypeConfiguration<CustomerDocument>
{
    public void Configure(EntityTypeBuilder<CustomerDocument> builder)
    {
        builder.ToTable("CustomerDocuments");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.FileName).IsRequired().HasMaxLength(260);
        builder.Property(d => d.StoredPath).IsRequired().HasMaxLength(1024);
        builder.Property(d => d.MimeType).HasMaxLength(128);
        builder.Property(d => d.UploadedOnUtc).IsRequired();

        builder.HasIndex(d => d.CustomerRequestId);
        builder.HasIndex(d => d.TenantId);
    }
}
