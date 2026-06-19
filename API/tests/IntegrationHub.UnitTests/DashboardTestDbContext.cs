using IntegrationHub.Application.Abstractions.Security;
using IntegrationHub.Application.Abstractions.Tenancy;
using IntegrationHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.UnitTests;

/// <summary>
/// Builds an EF Core InMemory <see cref="IntegrationHubDbContext"/> for the DbContext-backed
/// dashboard tests. The tenant context is left unresolved so the ambient tenant query filter is a
/// no-op (the dashboard query service scopes explicitly with <c>IgnoreQueryFilters</c>). Each call
/// gets an isolated database (unique name) so tests do not share state.
/// </summary>
internal static class DashboardTestDbContext
{
    private sealed class UnresolvedTenantContext : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
        public string TenantIdentifier => string.Empty;
        public bool IsResolved => false;
        public void Set(Guid tenantId, string tenantIdentifier) { }
    }

    private sealed class SystemActorAccessor : IActorAccessor
    {
        public string GetCurrentActor() => "system";
    }

    public static IntegrationHubDbContext Create()
    {
        var options = new DbContextOptionsBuilder<IntegrationHubDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new IntegrationHubDbContext(options, new UnresolvedTenantContext(), new SystemActorAccessor());
    }

    /// <summary>
    /// Persists entities with the supplied audit timestamps intact. The DbContext's
    /// <c>SaveChangesAsync(CancellationToken)</c> override stamps Created/Updated to "now" on insert,
    /// which would clobber the historical dates the window/trend/SLA tests depend on. The
    /// <c>SaveChangesAsync(bool, CancellationToken)</c> overload is NOT overridden, so calling it
    /// persists without stamping and the entities are stored exactly as given.
    /// </summary>
    public static async Task SeedAsync(
        IntegrationHubDbContext db,
        params Domain.Entities.AuditableEntity[] rows)
    {
        foreach (var entity in rows)
        {
            db.Add(entity);
        }

        await db.SaveChangesAsync(acceptAllChangesOnSuccess: true, CancellationToken.None);
        db.ChangeTracker.Clear();
    }
}
