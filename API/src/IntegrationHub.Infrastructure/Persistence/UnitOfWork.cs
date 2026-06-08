using IntegrationHub.Application.Abstractions.Persistence;

namespace IntegrationHub.Infrastructure.Persistence;

/// <summary>
/// EF Core unit of work. Delegates to the scoped <see cref="IntegrationHubDbContext"/>,
/// so every repository sharing that context commits in a single transaction.
/// </summary>
internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly IntegrationHubDbContext _dbContext;

    public UnitOfWork(IntegrationHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
