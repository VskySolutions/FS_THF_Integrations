using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntegrationHub.Infrastructure.Persistence.Configurations;

internal sealed class TenantRoleConfiguration : IEntityTypeConfiguration<TenantRole>
{
    public void Configure(EntityTypeBuilder<TenantRole> builder)
    {
        builder.ToTable("TenantRoles");

        builder.HasKey(tr => tr.Id);

        builder.HasIndex(tr => tr.TenantId);

        // A role is assigned to a tenant at most once.
        builder.HasIndex(tr => new { tr.TenantId, tr.RoleId }).IsUnique().HasFilter("[Deleted] = 0");
    }
}
