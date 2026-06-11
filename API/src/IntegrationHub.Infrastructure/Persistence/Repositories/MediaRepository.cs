using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence.Repositories;

internal sealed class MediaRepository : IMediaRepository
{
    private readonly IntegrationHubDbContext _dbContext;

    public MediaRepository(IntegrationHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Media?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Media.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task AddAsync(Media media, CancellationToken cancellationToken = default)
        => await _dbContext.Media.AddAsync(media, cancellationToken);

    public void Update(Media media) => _dbContext.Media.Update(media);
}
