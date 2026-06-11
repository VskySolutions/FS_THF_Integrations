using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntegrationHub.Infrastructure.Persistence.Configurations;

internal sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("Persons");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.PersonCode).IsRequired().HasMaxLength(64);
        builder.HasIndex(p => p.PersonCode).IsUnique().HasFilter("[Deleted] = 0");

        // Personal information
        builder.Property(p => p.FirstName).HasMaxLength(100);
        builder.Property(p => p.MiddleName).HasMaxLength(100);
        builder.Property(p => p.LastName).HasMaxLength(100);
        builder.Property(p => p.DisplayName).HasMaxLength(200);
        builder.Property(p => p.PreferredName).HasMaxLength(100);
        builder.Property(p => p.Gender).HasMaxLength(32);
        builder.Property(p => p.MaritalStatus).HasMaxLength(32);
        builder.Property(p => p.Nationality).HasMaxLength(100);
        builder.Property(p => p.TimeZone).HasMaxLength(64);
        builder.Property(p => p.Language).HasMaxLength(32);

        // Contact information
        builder.Property(p => p.PrimaryEmail).HasMaxLength(256);
        builder.Property(p => p.SecondaryEmail).HasMaxLength(256);
        builder.Property(p => p.MobileNumber).HasMaxLength(32);
        builder.Property(p => p.CountryCode).HasMaxLength(8);
        builder.Property(p => p.AlternateMobileNumber).HasMaxLength(32);
        builder.Property(p => p.EmergencyContactName).HasMaxLength(150);
        builder.Property(p => p.EmergencyContactRelationship).HasMaxLength(64);
        builder.Property(p => p.EmergencyContactNumber).HasMaxLength(32);

        // Professional information
        builder.Property(p => p.EmployeeCode).HasMaxLength(64);
        builder.Property(p => p.JobTitle).HasMaxLength(128);
        builder.Property(p => p.Department).HasMaxLength(128);
        builder.Property(p => p.Organization).HasMaxLength(128);

        // Social information
        builder.Property(p => p.LinkedInUrl).HasMaxLength(512);
        builder.Property(p => p.TwitterUrl).HasMaxLength(512);
        builder.Property(p => p.FacebookUrl).HasMaxLength(512);
        builder.Property(p => p.InstagramUrl).HasMaxLength(512);
        builder.Property(p => p.WebsiteUrl).HasMaxLength(512);

        builder.Property(p => p.Notes).HasMaxLength(2000);

        // Primary address + profile image (no cascade — addresses/media are shared, reusable).
        builder.HasOne(p => p.Address)
            .WithMany()
            .HasForeignKey(p => p.AddressId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.ProfileMedia)
            .WithMany()
            .HasForeignKey(p => p.ProfileMediaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
