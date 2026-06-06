using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence;

/// <summary>
/// EF Core unit of work for the IntegrationHub application schema. Owns the
/// application tables (jobs, logs, retry queue, mapping configurations, audit
/// trail). Schema migrations are applied by the Integration API on startup;
/// the Background Worker and MCP Server are read/write consumers only.
/// </summary>
public class IntegrationHubDbContext : DbContext
{
    public IntegrationHubDbContext(DbContextOptions<IntegrationHubDbContext> options)
        : base(options)
    {
    }

    public DbSet<IntegrationJob> IntegrationJobs => Set<IntegrationJob>();

    public DbSet<IntegrationLog> IntegrationLogs => Set<IntegrationLog>();

    public DbSet<RetryQueueEntry> RetryQueue => Set<RetryQueueEntry>();

    public DbSet<MappingConfiguration> MappingConfigurations => Set<MappingConfiguration>();

    public DbSet<AuditTrailEntry> AuditTrail => Set<AuditTrailEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IntegrationHubDbContext).Assembly);
    }
}
