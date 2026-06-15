using FluentAssertions;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Tenancy;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationHub.IntegrationTests;

// WO-35: repository operations against the real SQL Server + migration idempotency.
[Collection("Api")]
public class RepositoryIntegrationTests
{
    private readonly IntegrationHubApiFactory _factory;

    public RepositoryIntegrationTests(IntegrationHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task IntegrationJob_round_trips_and_transitions_status()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;

        // Scope to the seeded system tenant so TenantId is stamped on insert.
        var tenant = (await sp.GetRequiredService<ITenantRepository>().ListAsync()).First();
        sp.GetRequiredService<ITenantContext>().Set(tenant.Id, tenant.Identifier);

        var jobs = sp.GetRequiredService<IIntegrationJobRepository>();
        var unitOfWork = sp.GetRequiredService<IUnitOfWork>();

        var job = new IntegrationJob
        {
            Id = Guid.NewGuid(),
            InterfaceName = "ExpenseImport",
            Direction = IntegrationDirection.Inbound,
            SourceSystem = SystemName.Concur,
            TargetSystem = SystemName.Maconomy,
            Status = IntegrationJobStatus.Created,
        };
        await jobs.AddAsync(job);
        await unitOfWork.SaveChangesAsync();

        var loaded = await jobs.GetByIdAsync(job.Id);
        loaded.Should().NotBeNull();
        loaded!.TenantId.Should().Be(tenant.Id); // stamped from tenant context

        loaded.Status = IntegrationJobStatus.Completed;
        loaded.CompletedAtUtc = DateTime.UtcNow;
        jobs.Update(loaded);
        await unitOfWork.SaveChangesAsync();

        var completed = await jobs.ListByStatusAsync(IntegrationJobStatus.Completed);
        completed.Should().Contain(j => j.Id == job.Id);
    }

    [Fact]
    public async Task All_migrations_applied_with_none_pending()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationHubDbContext>();

        var applied = await db.Database.GetAppliedMigrationsAsync();
        var pending = await db.Database.GetPendingMigrationsAsync();

        applied.Should().Contain(m => m.Contains("InitialCreate"));
        pending.Should().BeEmpty();
    }

    [Fact]
    public void AuditTrail_repository_is_append_only()
    {
        // The contract exposes no update or delete path (REQ-INF-009 / AC-INF-009.3).
        var methods = typeof(IAuditTrailRepository).GetMethods().Select(m => m.Name);
        methods.Should().Contain("AddAsync");
        methods.Should().NotContain(n => n.Contains("Update") || n.Contains("Remove") || n.Contains("Delete"));
    }
}
