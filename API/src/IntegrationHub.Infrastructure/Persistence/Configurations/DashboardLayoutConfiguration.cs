using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntegrationHub.Infrastructure.Persistence.Configurations;

internal sealed class DashboardLayoutConfiguration : IEntityTypeConfiguration<DashboardLayout>
{
    public void Configure(EntityTypeBuilder<DashboardLayout> builder)
    {
        builder.ToTable("DashboardLayouts");

        builder.HasKey(d => d.Id);

        // One active layout per user.
        builder.HasIndex(d => d.UserId).IsUnique().HasFilter("[Deleted] = 0");

        builder.Property(d => d.WidgetOrderJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(d => d.HiddenWidgetsJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(d => d.CollapsedWidgetsJson).HasColumnType("nvarchar(max)").IsRequired();
    }
}
