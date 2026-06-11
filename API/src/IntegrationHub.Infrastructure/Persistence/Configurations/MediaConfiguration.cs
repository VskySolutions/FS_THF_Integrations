using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntegrationHub.Infrastructure.Persistence.Configurations;

internal sealed class MediaConfiguration : IEntityTypeConfiguration<Media>
{
    public void Configure(EntityTypeBuilder<Media> builder)
    {
        builder.ToTable("Media");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.MediaType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.MediaCategory).IsRequired().HasConversion<string>().HasMaxLength(20);

        builder.Property(m => m.OriginalFileName).IsRequired().HasMaxLength(260);
        builder.Property(m => m.StoredFileName).IsRequired().HasMaxLength(260);
        builder.Property(m => m.FileExtension).HasMaxLength(20);
        builder.Property(m => m.MimeType).HasMaxLength(127);

        builder.Property(m => m.StorageProvider).HasMaxLength(50);
        builder.Property(m => m.RelativePath).HasMaxLength(1024);
        builder.Property(m => m.PublicUrl).HasMaxLength(2048);
        builder.Property(m => m.ThumbnailUrl).HasMaxLength(2048);
        builder.Property(m => m.PreviewUrl).HasMaxLength(2048);
        builder.Property(m => m.Resolution).HasMaxLength(50);
    }
}
