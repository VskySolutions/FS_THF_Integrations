using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence.Repositories;

internal sealed class SavedViewRepository : ISavedViewRepository
{
    private readonly IntegrationHubDbContext _dbContext;

    public SavedViewRepository(IntegrationHubDbContext dbContext) => _dbContext = dbContext;

    public Task AddAsync(SavedView view, CancellationToken cancellationToken = default)
        => _dbContext.SavedViews.AddAsync(view, cancellationToken).AsTask();

    public Task<SavedView?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.SavedViews.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

    public void Update(SavedView view) => _dbContext.SavedViews.Update(view);

    public void Remove(SavedView view) => _dbContext.SavedViews.Remove(view);

    public async Task<IReadOnlyList<SavedView>> ListForUserAsync(Guid userId, string listPage, CancellationToken cancellationToken = default)
        => await _dbContext.SavedViews
            .Where(v => v.ListPage == listPage && (v.UserId == userId || v.IsShared))
            // Private views first, then shared, each alphabetical.
            .OrderByDescending(v => v.UserId == userId)
            .ThenBy(v => v.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SavedView>> ListSharedAsync(CancellationToken cancellationToken = default)
        => await _dbContext.SavedViews
            .Where(v => v.IsShared)
            .OrderBy(v => v.ListPage)
            .ThenBy(v => v.Name)
            .ToListAsync(cancellationToken);
}
