using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntegrationHub.Infrastructure.Persistence.Configurations;

internal sealed class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("Addresses");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.AddressType).IsRequired().HasConversion<string>().HasMaxLength(20);

        builder.Property(a => a.AddressLine1).HasMaxLength(256);
        builder.Property(a => a.AddressLine2).HasMaxLength(256);
        builder.Property(a => a.Landmark).HasMaxLength(128);
        builder.Property(a => a.Area).HasMaxLength(128);
        builder.Property(a => a.BuildingName).HasMaxLength(128);
        builder.Property(a => a.FloorNumber).HasMaxLength(32);
        builder.Property(a => a.UnitNumber).HasMaxLength(32);

        builder.Property(a => a.CountryCode).HasMaxLength(3);
        builder.Property(a => a.CountryName).HasMaxLength(100);
        builder.Property(a => a.StateCode).HasMaxLength(10);
        builder.Property(a => a.StateName).HasMaxLength(100);
        builder.Property(a => a.CityCode).HasMaxLength(10);
        builder.Property(a => a.CityName).HasMaxLength(100);
        builder.Property(a => a.PostalCode).HasMaxLength(20);

        builder.Property(a => a.ValidationSource).HasMaxLength(50);
    }
}
