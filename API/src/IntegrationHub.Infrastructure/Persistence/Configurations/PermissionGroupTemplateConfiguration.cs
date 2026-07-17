using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntegrationHub.Infrastructure.Persistence.Configurations;

internal sealed class PermissionGroupTemplateConfiguration : IEntityTypeConfiguration<PermissionGroupTemplate>
{
    private static readonly DateTime SeedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<PermissionGroupTemplate> builder)
    {
        builder.ToTable("PermissionGroupTemplates");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).IsRequired().HasMaxLength(150);
        builder.Property(t => t.Description).HasMaxLength(500);
        builder.Property(t => t.IsSeeded).IsRequired();
        builder.Property(t => t.PermissionKeysJson).IsRequired().HasMaxLength(4000);

        builder.HasIndex(t => t.Name);

        // The platform-standard seeded templates (permission keys mapped to the live catalogue).
        builder.HasData(
            Seed("22222222-2222-2222-2222-222222222203", "Tenant Configurator", "Configure tenant settings.", "[\"tenants.read\",\"tenants.write\"]"),
            Seed("22222222-2222-2222-2222-222222222204", "Customer Reviewer", "Enrich and review customer requests.", "[\"customers.review\"]"),
            Seed("22222222-2222-2222-2222-222222222205", "Customer Approver", "Review and approve customer requests.", "[\"customers.review\",\"customers.approve\"]"));
    }

    private static PermissionGroupTemplate Seed(string id, string name, string description, string keysJson) => new()
    {
        Id = Guid.Parse(id),
        Name = name,
        Description = description,
        IsSeeded = true,
        PermissionKeysJson = keysJson,
        CreatedOnUtc = SeedTimestamp,
        UpdatedOnUtc = SeedTimestamp,
        Deleted = false,
    };
}
