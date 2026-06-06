using IntegrationHub.Application.Abstractions.Tenancy;
using IntegrationHub.Domain.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence;

/// <summary>
/// EF Core unit of work for the IntegrationHub application schema. Owns the
/// application tables (jobs, logs, retry queue, mapping configurations, audit
/// trail). Schema migrations are applied by the Integration API on startup;
/// the Background Worker and MCP Server are read/write consumers only.
/// <para>
/// Tenant isolation is enforced here: tenant-scoped entities carry a global query
/// filter on the resolved <see cref="ITenantContext.TenantId"/>, and inserts are
/// stamped with the active tenant. When no tenant is resolved (background/global
/// operations such as the retry scheduler, or design-time), the filter is a no-op.
/// </para>
/// </summary>
public class IntegrationHubDbContext : DbContext, IDataProtectionKeyContext
{
    private readonly ITenantContext _tenantContext;

    public IntegrationHubDbContext(DbContextOptions<IntegrationHubDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<IntegrationJob> IntegrationJobs => Set<IntegrationJob>();

    public DbSet<IntegrationLog> IntegrationLogs => Set<IntegrationLog>();

    public DbSet<RetryQueueEntry> RetryQueue => Set<RetryQueueEntry>();

    public DbSet<MappingConfiguration> MappingConfigurations => Set<MappingConfiguration>();

    public DbSet<AuditTrailEntry> AuditTrail => Set<AuditTrailEntry>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<TenantApiConfiguration> TenantApiConfigurations => Set<TenantApiConfiguration>();

    public DbSet<JobScheduleConfiguration> JobScheduleConfigurations => Set<JobScheduleConfiguration>();

    public DbSet<User> Users => Set<User>();

    public DbSet<UserTenantRole> UserTenantRoles => Set<UserTenantRole>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>Data Protection key ring storage (Multi-Tenancy ADR-002).</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IntegrationHubDbContext).Assembly);

        // Tenant isolation filters. Filter is bypassed when no tenant is resolved so
        // global background operations (e.g. the cross-tenant retry scheduler) still work.
        modelBuilder.Entity<IntegrationJob>().HasQueryFilter(e => !_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<IntegrationLog>().HasQueryFilter(e => !_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<RetryQueueEntry>().HasQueryFilter(e => !_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<AuditTrailEntry>().HasQueryFilter(e => !_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTenant();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampTenant();
        return base.SaveChanges();
    }

    /// <summary>Stamps the resolved tenant id on newly added tenant-scoped entities.</summary>
    private void StampTenant()
    {
        if (!_tenantContext.IsResolved)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added)
            {
                continue;
            }

            switch (entry.Entity)
            {
                case IntegrationJob job when job.TenantId == Guid.Empty:
                    job.TenantId = _tenantContext.TenantId;
                    break;
                case IntegrationLog log when log.TenantId == Guid.Empty:
                    log.TenantId = _tenantContext.TenantId;
                    break;
                case RetryQueueEntry retry when retry.TenantId == Guid.Empty:
                    retry.TenantId = _tenantContext.TenantId;
                    break;
                case AuditTrailEntry audit when audit.TenantId == Guid.Empty:
                    audit.TenantId = _tenantContext.TenantId;
                    break;
            }
        }
    }
}
