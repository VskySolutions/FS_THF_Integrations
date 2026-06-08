using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntegrationHub.Infrastructure.Persistence.Configurations;

internal sealed class JobScheduleConfigurationConfiguration : IEntityTypeConfiguration<JobScheduleConfiguration>
{
    // Fixed seed identifiers and timestamp (HasData requires deterministic values).
    private static readonly DateTime SeedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<JobScheduleConfiguration> builder)
    {
        builder.ToTable("JobScheduleConfigurations");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.JobName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(j => j.CronExpression)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(j => j.IsActive)
            .IsRequired();

        builder.Property(j => j.UpdatedDate)
            .IsRequired();

        builder.HasIndex(j => j.JobName).IsUnique();

        // Default recurring schedules loaded by the HangfireJobScheduler.
        builder.HasData(
            Seed("11111111-1111-1111-1111-111111111101", "ExpenseImportJob", "0 */2 * * *"),
            Seed("11111111-1111-1111-1111-111111111102", "InvoiceImportJob", "15 */2 * * *"),
            Seed("11111111-1111-1111-1111-111111111103", "VendorPaymentImportJob", "30 */2 * * *"),
            Seed("11111111-1111-1111-1111-111111111104", "RetryFailedJobsJob", "*/5 * * * *"));
    }

    private static JobScheduleConfiguration Seed(string id, string jobName, string cron) => new()
    {
        Id = Guid.Parse(id),
        JobName = jobName,
        CronExpression = cron,
        IsActive = true,
        UpdatedDate = SeedTimestamp,
    };
}
